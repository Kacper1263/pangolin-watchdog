using System.ComponentModel.DataAnnotations;

namespace PangolinWatchdog.BO;

public class GlobalWhitelistedIp
{
    [Key]
    public long Id { get; set; }
    
    [Required]
    [MaxLength(45)]
    public string IpAddress { get; set; } = string.Empty;
    
    public string? Name { get; set; } = string.Empty;
}