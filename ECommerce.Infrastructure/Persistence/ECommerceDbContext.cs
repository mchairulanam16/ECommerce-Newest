using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ECommerce.Domain.Entities;

namespace ECommerce.Infrastructure.Persistence
{
    public class ECommerceDbContext : DbContext
    {
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<InventoryItem> Inventory => Set<InventoryItem>();

        public ECommerceDbContext(DbContextOptions<ECommerceDbContext> options)
            : base(options)
        {
        }

        /*protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            b.Entity<Order>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                      .HasDefaultValueSql("UUID()");

                entity.HasIndex(x => x.CreatedAt);

                entity.Property(x => x.RowVersion)
                      .IsRowVersion();
            });


            b.Entity<InventoryItem>(entity =>
            {
                entity.HasKey(x => x.Sku);

                entity.Property(x => x.Sku)
                      .HasMaxLength(50);

                entity.Property(x => x.RowVersion)
                      .IsRowVersion()
                      .IsConcurrencyToken();

                entity.HasData(
                    new
                    {
                        Sku = "SKU-IPHONE-15",
                        ActualQty = 100,
                        ReservedQty = 0
                    },
                    new
                    {
                        Sku = "SKU-SAMSUNG-S24",
                        ActualQty = 50,
                        ReservedQty = 0
                    }
                );
            });
        }*/

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            b.Entity<Order>(entity =>
            {
                entity.HasKey(x => x.Id);

                // enum → string (lebih aman & readable)
                entity.Property(x => x.Status)
                      .HasConversion<string>()
                      .HasMaxLength(20);

                entity.HasIndex(x => x.PaymentExternalId)
                      .IsUnique();

                entity.HasIndex(x => x.CreatedAt);

                entity.Property(x => x.RowVersion)
                      .IsRowVersion()
                      .IsConcurrencyToken();

                // RELATIONSHIP
                entity.HasMany(x => x.Items)
                      .WithOne()
                      .HasForeignKey(x => x.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });


            b.Entity<OrderItem>(entity =>
            {
                // composite key
                entity.HasKey(x => new { x.OrderId, x.Sku });

                entity.Property(x => x.Sku)
                      .HasMaxLength(50);

                entity.Property(x => x.Qty)
                      .IsRequired();
            });


            b.Entity<InventoryItem>(entity =>
            {
                entity.HasKey(x => x.Sku);

                entity.Property(x => x.Sku)
                      .HasMaxLength(50);

                entity.Property(x => x.RowVersion)
                      .IsRowVersion()
                      .IsConcurrencyToken();

                entity.HasData(
                    new
                    {
                        Sku = "A1",
                        ActualQty = 100,
                        ReservedQty = 0
                    },
                    new
                    {
                        Sku = "B2",
                        ActualQty = 50,
                        ReservedQty = 0
                    }
                );
            });
        }

    }
}
