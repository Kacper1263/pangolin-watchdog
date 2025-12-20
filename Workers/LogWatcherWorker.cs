using Microsoft.EntityFrameworkCore;
using PangolinWatchdog.Data;
using PangolinWatchdog.Services;
using System.Text.RegularExpressions;
using PangolinWatchdog.DTO.Pangolin;
using PangolinWatchdog.Services.Pangolin;

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
        var timeEnd = DateTime.UtcNow;
        var timeStart = timeEnd.AddMinutes(-2);

        if (firstRun)
        {
            firstRun = false;
            timeStart = timeEnd.AddHours(-24);
        }

        var logs = await pangolin.FetchLogsAsync(config, timeStart, timeEnd);

        var newLogs = logs
            .Where(l => l.Id > config.LastProcessedLogId)
            .OrderBy(l => l.Id) 
            .ToList();

        if (!newLogs.Any()) return;

        _logger.LogInformation("Fetched {Count} new logs. Analyzing...", newLogs.Count);

        var rules = await db.Rules
            .Include(r => r.TargetResource)
            .Include(r => r.ExcludedResources)
            .Where(r => r.IsActive)
            .ToListAsync(token);
        
        var regexCache = new Dictionary<long, Regex>();

        var maxProcessedId = config.LastProcessedLogId;

        foreach (var log in newLogs)
        {
            foreach (var rule in rules)
            {
                var isResourceMatch = await IsResourceMatch(db, rule, log, token);
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

    private async Task<bool> IsResourceMatch(AppDbContext db, WatchdogRule rule, PangolinLogEntry log, CancellationToken token)
    {
        if (rule.IsGlobal)
        {
            if (!rule.ExcludedResources.Any()) return true;
            
            var excludedResourceIds = rule.ExcludedResources.Select(e => e.ResourceId).ToList();
            var logResource = await db.Resources.FirstOrDefaultAsync(r => r.PangolinResourceId == log.ResourceId, token);
            
            return logResource == null || !excludedResourceIds.Contains(logResource.Id);
        }
        else
        {
            if (rule.TargetResourceId == null) return false;
            
            var targetResource = await db.Resources.FirstOrDefaultAsync(r => r.Id == rule.TargetResourceId, token);
            if (targetResource == null) return false;
            
            return targetResource.PangolinResourceId == log.ResourceId;
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
        var resource = await db.Resources.FirstOrDefaultAsync(r => r.PangolinResourceId == log.ResourceId, token);
        if (resource == null)
        {
            _logger.LogWarning("Resource {ResId} not found in local DB, skipping ban for IP {Ip}", log.ResourceId, log.Ip);
            return;
        }

        var alreadyBanned = await db.BannedIps.AnyAsync(
            b => b.IpAddress == log.Ip && 
                 b.ExpiresAt > DateTime.Now && 
                 b.ResourceId == resource.Id, 
            token);
            
        if (alreadyBanned) return;

        var duration = rule.BanDurationMinutes ?? config.DefaultBanDurationMinutes;
        
        var ban = new BannedIp
        {
            IpAddress = log.Ip,
            ResourceId = resource.Id,
            Reason = $"Rule: {rule.Name} (Path: {log.Path})",
            ExpiresAt = DateTime.Now.AddMinutes(duration)
        };

        await pangolin.BanIpAsync(config, log.Ip, log.ResourceId, duration, ban.Reason);
        
        db.BannedIps.Add(ban);
        await db.SaveChangesAsync(token);
        
        _logger.LogWarning("BANNED IP: {Ip} for accessing {Path} on Resource {Res}", log.Ip, log.Path, log.ResourceId);
    }
}