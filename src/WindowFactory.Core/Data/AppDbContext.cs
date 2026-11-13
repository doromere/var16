using System.Text;
using Microsoft.EntityFrameworkCore;
using WindowFactory.Core.Models;

namespace WindowFactory.Core.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ProfileSystem> ProfileSystems => Set<ProfileSystem>();
    public DbSet<GlazingType> GlazingTypes => Set<GlazingType>();
    public DbSet<Homeowner> Homeowners => Set<Homeowner>();
    public DbSet<WindowOrder> WindowOrders => Set<WindowOrder>();
    public DbSet<WindowOrderUnit> WindowOrderUnits => Set<WindowOrderUnit>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.Property(x => x.Login).HasMaxLength(50).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Login).IsUnique();
            e.HasOne(x => x.Role).WithMany(r => r.Users).HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ProfileSystem>(e =>
        {
            e.ToTable("profile_systems");
            e.Property(x => x.BrandTitle).HasMaxLength(100).IsRequired();
            e.Property(x => x.PricePerMeter).HasPrecision(10, 2);
        });

        b.Entity<GlazingType>(e =>
        {
            e.ToTable("glazing_types");
            e.Property(x => x.GlazingName).HasMaxLength(100).IsRequired();
            e.Property(x => x.PricePerM2).HasPrecision(10, 2);
        });

        b.Entity<Homeowner>(e =>
        {
            e.ToTable("homeowners");
            e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            e.Property(x => x.DeliveryAddress).HasMaxLength(250).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
        });

        b.Entity<WindowOrder>(e =>
        {
            e.ToTable("window_orders");
            e.Property(x => x.OrderNum).HasMaxLength(20).IsRequired();
            e.Property(x => x.TotalPrice).HasPrecision(12, 2);
            e.Property(x => x.Status).HasMaxLength(30).IsRequired();
            e.Property(x => x.CancelReason).HasMaxLength(500);
            e.HasIndex(x => x.OrderNum).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Homeowner).WithMany().HasForeignKey(x => x.HomeownerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<WindowOrderUnit>(e =>
        {
            e.ToTable("window_order_units");
            e.Property(x => x.UnitCalculatedCost).HasPrecision(12, 2);
            e.HasOne(x => x.WindowOrder).WithMany(o => o.OrderItems).HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ProfileSystem).WithMany().HasForeignKey(x => x.ProfileSystemId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.GlazingType).WithMany().HasForeignKey(x => x.GlazingTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Имена столбцов в snake_case (id, role_id, price_per_m2 ...)
        foreach (var entity in b.Model.GetEntityTypes())
            foreach (var prop in entity.GetProperties())
                prop.SetColumnName(ToSnakeCase(prop.Name));
    }

    private static string ToSnakeCase(string name)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(name[i]));
        }
        return sb.ToString();
    }
}
