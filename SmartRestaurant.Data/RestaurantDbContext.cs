using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartRestaurant.Core.Entities;

namespace SmartRestaurant.Data;

/// <summary>
/// Entity Framework Core 8 relational database context for SmartRestaurant.
/// Configures 3NF schema, decimal precision, foreign key cascades, and optimistic concurrency tokens.
/// </summary>
public class RestaurantDbContext : DbContext
{
    public RestaurantDbContext(DbContextOptions<RestaurantDbContext> options)
        : base(options)
    {
    }

    public DbSet<DiningArea> DiningAreas => Set<DiningArea>();
    public DbSet<DiningTable> DiningTables => Set<DiningTable>();
    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<WorkShift> WorkShifts => Set<WorkShift>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. DiningArea
        modelBuilder.Entity<DiningArea>(entity =>
        {
            entity.HasKey(a => a.AreaId);
            entity.Property(a => a.AreaName).HasMaxLength(50).IsRequired();
            entity.Property(a => a.FloorNumber).HasDefaultValue(1);
        });

        // 2. DiningTable
        modelBuilder.Entity<DiningTable>(entity =>
        {
            entity.HasKey(t => t.TableId);
            entity.Property(t => t.TableName).HasMaxLength(50).IsRequired();
            entity.Property(t => t.Capacity).IsRequired();
            entity.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(t => t.RowVersion).IsRowVersion();

            entity.HasOne(t => t.Area)
                  .WithMany(a => a.Tables)
                  .HasForeignKey(t => t.AreaId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 3. MenuCategory
        modelBuilder.Entity<MenuCategory>(entity =>
        {
            entity.HasKey(c => c.CategoryId);
            entity.Property(c => c.CategoryName).HasMaxLength(100).IsRequired();
        });

        // 4. MenuItem
        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.HasKey(m => m.MenuItemId);
            entity.Property(m => m.Name).HasMaxLength(150).IsRequired();
            entity.Property(m => m.Price).HasPrecision(18, 2).IsRequired();
            entity.Property(m => m.ImageUrl).HasMaxLength(500);
            entity.Property(m => m.IsAvailable).HasDefaultValue(true);

            entity.HasOne(m => m.Category)
                  .WithMany(c => c.MenuItems)
                  .HasForeignKey(m => m.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 5. Ingredient (Raw Materials)
        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.HasKey(i => i.IngredientId);
            entity.Property(i => i.Name).HasMaxLength(100).IsRequired();
            entity.Property(i => i.Unit).HasMaxLength(20).IsRequired();
            entity.Property(i => i.CurrentStock).HasPrecision(18, 3).IsConcurrencyToken().IsRequired();
            entity.Property(i => i.MinStock).HasPrecision(18, 3).HasDefaultValue(0);
        });

        // 6. Recipe (BOM Junction)
        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasKey(r => r.RecipeId);
            entity.Property(r => r.QuantityRequired).HasPrecision(18, 3).IsRequired();

            entity.HasOne(r => r.MenuItem)
                  .WithMany(m => m.Recipes)
                  .HasForeignKey(r => r.MenuItemId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.Ingredient)
                  .WithMany(i => i.Recipes)
                  .HasForeignKey(r => r.IngredientId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 7. Order
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.OrderId);
            entity.Property(o => o.Status).HasMaxLength(20).IsRequired();
            entity.Property(o => o.SubTotal).HasPrecision(18, 2);
            entity.Property(o => o.DiscountAmount).HasPrecision(18, 2);
            entity.Property(o => o.FinalTotal).HasPrecision(18, 2);

            entity.HasOne(o => o.Table)
                  .WithMany(t => t.Orders)
                  .HasForeignKey(o => o.TableId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 8. OrderItem
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(oi => oi.OrderItemId);
            entity.Property(oi => oi.Quantity).IsRequired();
            entity.Property(oi => oi.UnitPrice).HasPrecision(18, 2).IsRequired();
            entity.Property(oi => oi.CookingStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(oi => oi.Note).HasMaxLength(250);

            entity.HasOne(oi => oi.Order)
                  .WithMany(o => o.OrderItems)
                  .HasForeignKey(oi => oi.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(oi => oi.MenuItem)
                  .WithMany()
                  .HasForeignKey(oi => oi.MenuItemId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 9. Invoice
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(inv => inv.InvoiceId);
            entity.Property(inv => inv.InvoiceCode).HasMaxLength(50).IsRequired();
            entity.Property(inv => inv.VatRate).HasPrecision(5, 4);
            entity.Property(inv => inv.VatAmount).HasPrecision(18, 2);
            entity.Property(inv => inv.TotalDue).HasPrecision(18, 2);

            entity.HasOne(inv => inv.Order)
                  .WithOne(o => o.Invoice)
                  .HasForeignKey<Invoice>(inv => inv.OrderId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 10. Payment
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(p => p.PaymentId);
            entity.Property(p => p.Method).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(p => p.AmountPaid).HasPrecision(18, 2).IsRequired();

            entity.HasOne(p => p.Invoice)
                  .WithMany(inv => inv.Payments)
                  .HasForeignKey(p => p.InvoiceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 11. User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.UserId);
            entity.Property(u => u.Username).HasMaxLength(50).IsRequired();
            entity.Property(u => u.Role).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(u => u.FullName).HasMaxLength(100);
        });

        // 12. Customer (Loyalty Member - Strictly Unique Phone Number)
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(c => c.CustomerId);
            entity.HasIndex(c => c.PhoneNumber).IsUnique();
            entity.Property(c => c.PhoneNumber).HasMaxLength(20).IsRequired();
            entity.Property(c => c.FullName).HasMaxLength(100).IsRequired();
            entity.Property(c => c.Email).HasMaxLength(100);
            entity.Property(c => c.Tier).HasConversion<string>().HasMaxLength(20);
            entity.Property(c => c.TotalSpent).HasPrecision(18, 2);
        });

        // 13. WorkShift (Cashier Shift & Z-Report)
        modelBuilder.Entity<WorkShift>(entity =>
        {
            entity.HasKey(w => w.ShiftId);
            entity.Property(w => w.OpeningFloat).HasPrecision(18, 2);
            entity.Property(w => w.CashSales).HasPrecision(18, 2);
            entity.Property(w => w.CardSales).HasPrecision(18, 2);
            entity.Property(w => w.QrSales).HasPrecision(18, 2);
            entity.Property(w => w.TotalSales).HasPrecision(18, 2);
            entity.Property(w => w.ActualCashEnd).HasPrecision(18, 2);
            entity.Property(w => w.CashVariance).HasPrecision(18, 2);

            entity.HasOne(w => w.User)
                  .WithMany()
                  .HasForeignKey(w => w.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
