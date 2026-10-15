using Microsoft.EntityFrameworkCore;
using Belah_Backend.Models;

namespace Belah_Backend.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<ProductCategory> ProductCategories { get; set; }
        public DbSet<ProductVariant> ProductVariants { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<EmailNotification> EmailNotifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(u => u.Role)
                    .HasMaxLength(20)
                    .HasDefaultValue("Customer");
            });

            modelBuilder.Entity<Product>(entity =>
            {
                var utcNowDefault = Database.IsSqlServer() ? "SYSUTCDATETIME()" : "CURRENT_TIMESTAMP";
                entity.Property(p => p.Price).HasPrecision(18, 2);
                entity.Property(p => p.SalePrice).HasPrecision(18, 2);
                entity.Property(p => p.Sku).HasMaxLength(100);
                entity.Property(p => p.StockQuantity).HasDefaultValue(0);
                entity.Property(p => p.IsActive).HasDefaultValue(true);
                entity.Property(p => p.IsFeatured).HasDefaultValue(false);
                entity.Property(p => p.IsPriceApproved).HasDefaultValue(true);
                entity.Property(p => p.IsStockConfirmed).HasDefaultValue(true);
                entity.Property(p => p.Brand).HasMaxLength(160);
                entity.Property(p => p.ProductType).HasMaxLength(120);
                entity.Property(p => p.CreatedAtUtc).HasDefaultValueSql(utcNowDefault);
                entity.Property(p => p.UpdatedAtUtc).HasDefaultValueSql(utcNowDefault);
                entity.HasMany(p => p.Variants)
                    .WithOne(v => v.Product)
                    .HasForeignKey(v => v.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(p => p.ProductCategory)
                    .WithMany(c => c.Products)
                    .HasForeignKey(p => p.CategoryId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(p => p.Sku)
                    .IsUnique()
                    .HasFilter("[Sku] IS NOT NULL");
            });

            modelBuilder.Entity<ProductCategory>(entity =>
            {
                entity.HasIndex(c => c.Name).IsUnique();
                entity.HasIndex(c => c.Slug).IsUnique();
                entity.Property(c => c.IsActive).HasDefaultValue(true);
            });

            modelBuilder.Entity<ProductVariant>(entity =>
            {
                entity.Property(v => v.Price).HasPrecision(18, 2);
                entity.Property(v => v.SalePrice).HasPrecision(18, 2);
                entity.Property(v => v.Name).HasMaxLength(100);
                entity.Property(v => v.Sku).HasMaxLength(100);
                entity.HasIndex(v => v.Sku).IsUnique().HasFilter("[Sku] IS NOT NULL");
                entity.HasIndex(v => new { v.ProductId, v.Name }).IsUnique();
                entity.Property(v => v.IsActive).HasDefaultValue(true);
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(o => o.TotalAmount).HasPrecision(18, 2);
                entity.Property(o => o.DeliveryFee).HasPrecision(18, 2).HasDefaultValue(0m);
                entity.Property(o => o.Status).HasDefaultValue("Pending");
                entity.Property(o => o.PaymentStatus).HasDefaultValue("Pending");

                entity.HasIndex(o => o.OrderNumber).IsUnique();
                entity.HasIndex(o => o.PaymentReference)
                    .IsUnique()
                    .HasFilter("[PaymentReference] IS NOT NULL");
                entity.HasIndex(o => o.PaystackTransactionId)
                    .IsUnique()
                    .HasFilter("[PaystackTransactionId] IS NOT NULL");
                entity.HasIndex(o => o.GuestCapabilityHash)
                    .IsUnique()
                    .HasFilter("[GuestCapabilityHash] IS NOT NULL");
                entity.HasIndex(o => o.UserId);
                entity.HasIndex(o => o.OrderDate);
                entity.HasIndex(o => o.Email);

                entity.HasMany(o => o.OrderItems)
                      .WithOne(oi => oi.Order)
                      .HasForeignKey(oi => oi.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(o => o.User)
                    .WithMany()
                    .HasForeignKey(o => o.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.Property(oi => oi.UnitPrice).HasPrecision(18, 2);
                entity.Property(oi => oi.Subtotal).HasPrecision(18, 2);

                // When Product is deleted, do NOT cascade-delete historical OrderItems
                entity.HasOne(oi => oi.Product)
                      .WithMany()
                      .HasForeignKey(oi => oi.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(oi => oi.ProductVariant)
                      .WithMany()
                      .HasForeignKey(oi => oi.ProductVariantId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<EmailNotification>(entity =>
            {
                entity.Property(n => n.Currency).HasDefaultValue("ZAR");
                entity.Property(n => n.Status).HasDefaultValue("Pending");
                entity.Property(n => n.Attempts).HasDefaultValue(0);
                entity.Property(n => n.TotalAttempts).HasDefaultValue(0);
                entity.Property(n => n.CreatedAtUtc).HasDefaultValueSql(Database.IsSqlServer() ? "SYSUTCDATETIME()" : "CURRENT_TIMESTAMP");
                entity.Property(n => n.NextAttemptAtUtc).HasDefaultValueSql(Database.IsSqlServer() ? "SYSUTCDATETIME()" : "CURRENT_TIMESTAMP");
                entity.HasIndex(n => n.IdempotencyKey).IsUnique();
                entity.HasIndex(n => n.OrderId).IsUnique();
                entity.HasIndex(n => new { n.Status, n.NextAttemptAtUtc });
                entity.HasOne(n => n.Order)
                    .WithMany()
                    .HasForeignKey(n => n.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

        }
    }
}
