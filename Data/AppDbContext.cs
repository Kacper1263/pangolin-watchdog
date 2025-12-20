using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
namespace PangolinWatchdog.Data;


public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<BannedIp> BannedIps { get; set; }
    public DbSet<WatchdogRule> Rules { get; set; } 
    public DbSet<AppConfig> Configurations { get; set; }
    public DbSet<Resource> Resources { get; set; }
    public DbSet<RuleResourceExclusion> RuleResourceExclusions { get; set; }
}

public class BannedIp
{
    [Key]
    public long Id { get; set; }
    
    [Required]
    [MaxLength(45)]
    public string IpAddress { get; set; } = string.Empty;
    
    [MaxLength]
    public string Reason { get; set; } = string.Empty;
    
    [Required]
    public long ResourceId { get; set; }
    
    [ForeignKey(nameof(ResourceId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Resource Resource { get; set; } = null!;
    
    public DateTime BannedAt { get; set; } = DateTime.Now;
    public DateTime? ExpiresAt { get; set; }
}

public class AppConfig
{
    [Key]
    public int Id { get; set; } // Single row with Id = 1
    
    // Pangolin API Settings
    public string PangolinApiUrl { get; set; } = "https://api.pangolin.example.com/v1";
    public string PangolinOrgId { get; set; } = "main";
    public string PangolinApiToken { get; set; } = string.Empty;

    // Watchdog Cycle Settings
    public int LogPollingIntervalSeconds { get; set; } = 60;
    public int BanCleanupIntervalMinutes { get; set; } = 60;
    // 7 days by default
    public int DefaultBanDurationMinutes { get; set; } = 7 * 24 * 60;
    
    public long LastProcessedLogId { get; set; } = 0;
}

public class WatchdogRule
{
    [Key]
    public long Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Pattern { get; set; } = string.Empty;
    public bool UseRegex { get; set; }

    public bool IsGlobal { get; set; } = true;

    public long? TargetResourceId { get; set; }
    
    [ForeignKey(nameof(TargetResourceId))]
    [DeleteBehavior(DeleteBehavior.SetNull)]
    public Resource? TargetResource { get; set; }

    public List<RuleResourceExclusion> ExcludedResources { get; set; } = new();

    public int? BanDurationMinutes { get; set; } = 60;
    
    public bool IsActive { get; set; } = true;
}

public class Resource
{
    [Key]
    public long Id { get; set; }
    
    [Required]
    public long PangolinResourceId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(500)]
    public string FullDomain { get; set; } = string.Empty;
}

public class RuleResourceExclusion
{
    [Key]
    public long Id { get; set; }
    
    [Required]
    public long RuleId { get; set; }
    
    [ForeignKey(nameof(RuleId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public WatchdogRule Rule { get; set; } = null!;
    
    [Required]
    public long ResourceId { get; set; }
    
    [ForeignKey(nameof(ResourceId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Resource Resource { get; set; } = null!;
}

