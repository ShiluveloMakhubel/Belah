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
        public DbSet<User> Users { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

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
                entity.Property(p => p.Price).HasPrecision(18, 2);
                entity.Property(p => p.Sku).HasMaxLength(100);
                entity.Property(p => p.StockQuantity).HasDefaultValue(0);
                entity.Property(p => p.IsActive).HasDefaultValue(true);
                entity.HasIndex(p => p.Sku)
                    .IsUnique()
                    .HasFilter("[Sku] IS NOT NULL");
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(o => o.TotalAmount).HasPrecision(18, 2);
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
            });
        }
    }
}
