using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PangolinWatchdog.Data;

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