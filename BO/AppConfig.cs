using System.ComponentModel.DataAnnotations;

namespace PangolinWatchdog.Data;

public class AppConfig
{
    [Key]
    public int Id { get; set; } // Single row with Id = 1
    
    // Pangolin API Settings
    public string PangolinApiUrl { get; set; } = "https://api.pangolin.example.com/v1";
    public string PangolinOrgId { get; set; } = string.Empty;
    public string PangolinApiToken { get; set; } = string.Empty;

    // Watchdog Cycle Settings
    public int LogPollingIntervalSeconds { get; set; } = 60;
    public int BanCleanupIntervalMinutes { get; set; } = 60;
    // 7 days by default
    public int DefaultBanDurationMinutes { get; set; } = 7 * 24 * 60;
    
    public long LastProcessedLogId { get; set; } = 0;
}