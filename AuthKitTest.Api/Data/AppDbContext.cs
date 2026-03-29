using AuthKit.EntityFramework;
using AuthKitTest.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthKitTest.Api.Data;

public class AppDbContext : AuthDbContextBase
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasMaxLength(256);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).IsRequired().HasMaxLength(256);
            e.Property(u => u.FirstName).HasMaxLength(128);
            e.Property(u => u.LastName).HasMaxLength(128);

            e.HasMany(u => u.UserRoles)
             .WithOne()
             .HasForeignKey(ur => ur.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(u => u.UserPermissions)
             .WithOne()
             .HasForeignKey(up => up.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
