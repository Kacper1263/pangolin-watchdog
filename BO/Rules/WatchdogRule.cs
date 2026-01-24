using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PangolinWatchdog.Data;

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
    
    /// <summary>
    /// Minimum priority value for ban rules created by this watchdog rule.
    /// If set, the system will fill gaps between MinPriority and MaxPriority before adding new entries.
    /// If null, the system will always append new rules at the end (Max + 1).
    /// </summary>
    public long? MinPriority { get; set; }
    
    /// <summary>
    /// Used to allow declaring rules with higher priority (e.g., allow country) but still banning IPs by giving them lower (number) priority.
    /// If the next priority is equal or higher than MaxPriority, we will disable this rule.
    /// </summary>
    public long? MaxPriority { get; set; }
}