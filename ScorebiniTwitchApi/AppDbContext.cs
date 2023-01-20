using Microsoft.EntityFrameworkCore;
using ScorebiniTwitchApi.Models;

namespace ScorebiniTwitchApi
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<ScorebiniUserInfo> Users { get; set; }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<DateTime>().HaveConversion<DateTimeToIs08061Converter>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScorebiniUserInfo>().ToTable(nameof(Users));
        }
    }
}
