using Microsoft.EntityFrameworkCore;
using PangolinWatchdog.Data;
using PangolinWatchdog.Services;
using System.Text.RegularExpressions;

namespace PangolinWatchdog.Workers;

public class LogWatcherWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LogWatcherWorker> _logger;
    
    bool firstRun = true;

    public LogWatcherWorker(IServiceProvider serviceProvider, ILogger<LogWatcherWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Pangolin Watchdog Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delaySeconds = 60; // Default delay in case of error/missing config

            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var pangolin = scope.ServiceProvider.GetRequiredService<PangolinConnector>();

                    // Get Config
                    var config = await db.Configurations.FirstOrDefaultAsync(stoppingToken);
                    
                    if (config != null)
                    {
                        delaySeconds = config.LogPollingIntervalSeconds;

                        // Validate API URL before processing
                        if (!string.IsNullOrEmpty(config.PangolinApiUrl) && !string.IsNullOrEmpty(config.PangolinOrgId) && !string.IsNullOrEmpty(config.PangolinApiToken))
                        {
                            await ProcessLogs(db, pangolin, config, stoppingToken);
                        }
                        else
                        {
                            _logger.LogDebug("Worker idle: API URL or OrgId or Token not configured.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Catch all exceptions to prevent the Worker (and App) from crashing.
                _logger.LogError(ex, "Critical error in LogWatcherWorker loop. Retrying in {Sec} seconds.", delaySeconds);
            }

            try 
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
            }
            catch (TaskCanceledException) 
            {
                // Graceful shutdown
                break; 
            }
        }
        
        _logger.LogInformation("Pangolin Watchdog Worker stopped.");
    }

    private async Task ProcessLogs(AppDbContext db, PangolinConnector pangolin, AppConfig config, CancellationToken token)
    {
        // take logs with 2-minute buffer to avoid missing any
        var timeEnd = DateTime.UtcNow;
        var timeStart = timeEnd.AddMinutes(-2);

        if (firstRun)
        {
            firstRun = false;
            timeStart = timeEnd.AddHours(24); // on the first run, fetch last 24 hours to catch up
        }

        var logs = await pangolin.FetchLogsAsync(config, timeStart, timeEnd);

        // filter only new logs
        var newLogs = logs
            .Where(l => l.Id > config.LastProcessedLogId)
            .OrderBy(l => l.Id) 
            .ToList();

        if (!newLogs.Any()) return;

        _logger.LogInformation("Fetched {Count} new logs. Analyzing...", newLogs.Count);

        var rules = await db.Rules.Where(r => r.IsActive).ToListAsync(token);
        
        var regexCache = new Dictionary<long, Regex>();

        var maxProcessedId = config.LastProcessedLogId;

        foreach (var log in newLogs)
        {
            foreach (var rule in rules)
            {
                var isResourceMatch = IsResourceMatch(rule, log);
                if (!isResourceMatch) continue;

                var isPatternMatch = CheckPattern(rule, log.Path, regexCache);
                
                if (isPatternMatch)
                {
                    await BanUser(db, pangolin, config, log, rule, token);
                    break;
                }
            }
            
            if (log.Id > maxProcessedId) maxProcessedId = log.Id;
        }

        config.LastProcessedLogId = maxProcessedId;
        await db.SaveChangesAsync(token);
    }

    private bool IsResourceMatch(WatchdogRule rule, PangolinLogEntry log)
    {
        if (rule.IsGlobal)
        {
            if (string.IsNullOrEmpty(rule.ExcludedResourceNames)) return true;
            
            var excluded = rule.ExcludedResourceNames.Split(',', StringSplitOptions.TrimEntries);
            return !excluded.Contains(log.ResourceId.ToString()) && !excluded.Contains(log.ResourceName) && !excluded.Contains(log.Host);
        }
        else
        {
            if (string.IsNullOrEmpty(rule.TargetResourceName)) return false;
            
            return rule.TargetResourceName == log.ResourceId.ToString() || 
                   rule.TargetResourceName == log.ResourceName || 
                   rule.TargetResourceName == log.Host;
        }
    }

    private bool CheckPattern(WatchdogRule rule, string path, Dictionary<long, Regex> cache)
    {
        if (string.IsNullOrEmpty(path)) return false;

        if (rule.UseRegex)
        {
            if (!cache.ContainsKey(rule.Id))
            {
                try { cache[rule.Id] = new Regex(rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled); }
                catch { return false; } 
            }
            return cache[rule.Id].IsMatch(path);
        }
        else
        {
            return path.Equals(rule.Pattern, StringComparison.OrdinalIgnoreCase);
        }
    }

    private async Task BanUser(AppDbContext db, PangolinConnector pangolin, AppConfig config, PangolinLogEntry log, WatchdogRule rule, CancellationToken token)
    {
        // Check existing ban in LOCAL DB
        var alreadyBanned = await db.BannedIps.AnyAsync(
            b => b.IpAddress == log.Ip && 
                 b.ExpiresAt > DateTime.Now && 
                 b.ResourceId == log.ResourceId, 
            token);
            
        if (alreadyBanned) return;

        var duration = rule.BanDurationMinutes ?? config.DefaultBanDurationMinutes;
        
        var ban = new BannedIp
        {
            IpAddress = log.Ip,
            ResourceId = log.ResourceId,
            ResourceName = log.ResourceName,
            Reason = $"Rule: {rule.Name} (Path: {log.Path})",
            ExpiresAt = DateTime.Now.AddMinutes(duration)
        };

        // Send Ban to Pangolin API (this will calculate priority and PUT ip in rules)
        await pangolin.BanIpAsync(config, log.Ip, log.ResourceId, duration, ban.Reason);
        
        // Save to Local DB only if API call succeeded
        db.BannedIps.Add(ban);
        await db.SaveChangesAsync(token);
        
        _logger.LogWarning("BANNED IP: {Ip} for accessing {Path} on Resource {Res}", log.Ip, log.Path, log.ResourceId);
    }
}