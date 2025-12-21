using System.ComponentModel.DataAnnotations;

namespace PangolinWatchdog.Data;

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