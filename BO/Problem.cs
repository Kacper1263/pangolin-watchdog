using System.ComponentModel.DataAnnotations;

namespace PangolinWatchdog.Data;

public class Problem
{
    [Key]
    public long Id { get; set; }
    
    [Required]
    [MaxLength]
    public string Description { get; set; } = string.Empty;
    
    public DateTime DetectedAt { get; set; } = DateTime.Now;
}