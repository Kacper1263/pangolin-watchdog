using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PangolinWatchdog.Data;

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