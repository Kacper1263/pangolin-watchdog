using Microsoft.EntityFrameworkCore;
using PangolinWatchdog.Data;
using PangolinWatchdog.Services.Pangolin;

namespace PangolinWatchdog.Workers;

public class BanCleanupWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BanCleanupWorker> _logger;

    public BanCleanupWorker(IServiceProvider serviceProvider, ILogger<BanCleanupWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Ban Cleanup Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delayMinutes = 60;

            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var pangolin = scope.ServiceProvider.GetRequiredService<PangolinConnector>();

                    var config = await db.Configurations.FirstOrDefaultAsync(stoppingToken);
                    
                    if (config != null)
                    {
                        delayMinutes = config.BanCleanupIntervalMinutes;

                        if (!string.IsNullOrEmpty(config.PangolinApiUrl) && !string.IsNullOrEmpty(config.PangolinOrgId) && !string.IsNullOrEmpty(config.PangolinApiToken))
                        {
                            await ProcessExpiredBans(db, pangolin, config, stoppingToken);
                        }
                        else
                        {
                            _logger.LogDebug("Cleanup worker idle: API URL or OrgId or Token not configured.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error in BanCleanupWorker loop. Retrying in {Min} minutes.", delayMinutes);
            }

            try 
            {
                await Task.Delay(TimeSpan.FromMinutes(delayMinutes), stoppingToken);
            }
            catch (TaskCanceledException) 
            {
                break; 
            }
        }
        
        _logger.LogInformation("Ban Cleanup Worker stopped.");
    }

    private async Task ProcessExpiredBans(AppDbContext db, PangolinConnector pangolin, AppConfig config, CancellationToken token)
    {
        var now = DateTime.Now;
        
        var expiredBans = await db.BannedIps
            .Include(b => b.Resource)
            .Where(b => b.ExpiresAt != null && b.ExpiresAt <= now)
            .ToListAsync(token);

        if (!expiredBans.Any())
        {
            _logger.LogDebug("No expired bans found.");
            return;
        }

        _logger.LogInformation("Found {Count} expired bans. Processing...", expiredBans.Count);

        var groupedByResource = expiredBans.GroupBy(b => b.ResourceId);

        foreach (var resourceGroup in groupedByResource)
        {
            var resourceId = resourceGroup.Key;
            var resource = resourceGroup.First().Resource;
            var pangolinResourceId = resource.PangolinResourceId;

            var rules = await pangolin.GetRulesAsync(config, pangolinResourceId);

            foreach (var ban in resourceGroup)
            {
                var matchingRule = rules.FirstOrDefault(r => 
                    r.Match.Equals("IP", StringComparison.OrdinalIgnoreCase) && 
                    r.Value.Equals(ban.IpAddress, StringComparison.OrdinalIgnoreCase));

                if (matchingRule != null)
                {
                    var deleted = await pangolin.DeleteRuleAsync(config, pangolinResourceId, matchingRule.RuleId);
                    
                    if (deleted)
                    {
                        db.BannedIps.Remove(ban);
                        _logger.LogWarning("UNBANNED IP: {Ip} from Resource {Res}", ban.IpAddress, resource.Name);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to delete rule for IP {Ip} from Pangolin, keeping in local DB", ban.IpAddress);
                    }
                }
                else
                {
                    _logger.LogWarning("Rule not found for IP {Ip} on Resource {Res}, removing from local DB only", ban.IpAddress, resource.Name);
                    db.BannedIps.Remove(ban);
                }
            }
        }

        await db.SaveChangesAsync(token);
    }
}
