using Microsoft.EntityFrameworkCore;
namespace PangolinWatchdog.Data;


public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<BannedIp> BannedIps { get; set; }
    public DbSet<WatchdogRule> Rules { get; set; } 
    public DbSet<AppConfig> Configurations { get; set; }
    public DbSet<Resource> Resources { get; set; }
    public DbSet<RuleResourceExclusion> RuleResourceExclusions { get; set; }
    public DbSet<Problem> Problems { get; set; }
}