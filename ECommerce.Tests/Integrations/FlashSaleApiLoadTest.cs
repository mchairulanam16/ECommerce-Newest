using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Tests.Integration;
using FluentAssertions;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Tests.Load
{
    public class FlashSaleApiLoadTest : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory _factory;
        private readonly JsonSerializerOptions _jsonOptions;

        public FlashSaleApiLoadTest(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        [Fact(Skip = "Test Belum Selesai")]
        public async Task FlashSale_100ConcurrentRequests_ShouldNotOverReserve()
        {
            // Arrange
            /*await ResetDatabase();
            await SeedInventory("A1", 10, 0); // Only 10 in stock

            const int concurrentRequests = 100;
            var results = new ConcurrentBag<(HttpStatusCode Status, bool Success)>();

            var request = new
            {
                userId = Guid.NewGuid(), // Will be different for each request
                items = new[] { new { sku = "A1", qty = 1 } }
            };

            // Act - Simulate 100 concurrent requests
            var tasks = Enumerable.Range(0, concurrentRequests)
                .Select(async i =>
                {
                    var userRequest = new
                    {
                        userId = Guid.NewGuid(), // Unique user for each request
                        items = new[] { new { sku = "A1", qty = 1 } }
                    };

                    try
                    {
                        var response = await _client.PostAsJsonAsync("/api/orders", userRequest);
                        var success = response.StatusCode == HttpStatusCode.Created;
                        results.Add((response.StatusCode, success));
                    }
                    catch (Exception)
                    {
                        results.Add((HttpStatusCode.InternalServerError, false));
                    }
                });*/
            // Arrange
            await ResetDatabase();
            await SeedInventory("A1", 10, 0);

            const int concurrentRequests = 100;
            var results = new ConcurrentBag<(HttpStatusCode Status, bool Success, string? ErrorMessage)>(); // Tambah ErrorMessage

            // Act - Simulate 100 concurrent requests
            var tasks = Enumerable.Range(0, concurrentRequests)
                .Select(async i =>
                {
                    var userRequest = new
                    {
                        userId = Guid.NewGuid(),
                        items = new[] { new { sku = "A1", qty = 1 } }
                    };

                    try
                    {
                        var response = await _client.PostAsJsonAsync("/api/orders", userRequest);
                        var content = await response.Content.ReadAsStringAsync(); // TAMBAH INI
                        var success = response.StatusCode == HttpStatusCode.Created;

                        // Log untuk 5 request pertama saja agar tidak terlalu banyak
                        if (i < 5)
                        {
                            Console.WriteLine($"Request {i}: Status={response.StatusCode}, Content={content}");
                        }

                        results.Add((response.StatusCode, success, content));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Request {i} exception: {ex.Message}");
                        results.Add((HttpStatusCode.InternalServerError, false, ex.Message));
                    }
                });

            await Task.WhenAll(tasks);

            // ANALYZE RESULTS
            Console.WriteLine("\n=== ANALYSIS ===");
            var statusGroups = results.GroupBy(r => r.Status);
            foreach (var group in statusGroups)
            {
                Console.WriteLine($"Status {group.Key}: {group.Count()} requests");
                // Tampilkan error message untuk 3 pertama
                foreach (var item in group.Take(3))
                {
                    Console.WriteLine($"  - {item.ErrorMessage}");
                }
            }

            await Task.WhenAll(tasks);

            // Assert
            var successCount = results.Count(r => r.Success);
            var failCount = results.Count(r => !r.Success);

            // Only 10 should succeed (stock limit)
            successCount.Should().Be(10);
            failCount.Should().Be(90);

            // All failures should be BadRequest (out of stock)
            var failStatuses = results.Where(r => !r.Success).Select(r => r.Status);
            failStatuses.Should().AllBeEquivalentTo(HttpStatusCode.BadRequest);

            // Verify total reserved = 10
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();
            var inventory = await db.Inventory.FirstOrDefaultAsync(i => i.Sku == "A1");

            inventory.ReservedQty.Should().Be(10);
            inventory.ActualQty.Should().Be(10); // Unchanged until payment
        }

        [Fact(Skip = "Test Belum Selesai")]
        public async Task FlashSale_SingleRequest_ShouldWork()
        {
            // Arrange
            await ResetDatabase();
            await SeedInventory("A1", 10, 0);

            // Act - Single request
            var request = new
            {
                userId = Guid.NewGuid(),
                items = new[] { new { sku = "A1", qty = 1 } }
            };

            var response = await _client.PostAsJsonAsync("/api/orders", request);
            var content = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"Status: {response.StatusCode}");
            Console.WriteLine($"Content: {content}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }
        // Helper methods
        private async Task ResetDatabase()
        {
            /*using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();*/
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

            // Hapus semua data
            db.Inventory.RemoveRange(db.Inventory);
            await db.SaveChangesAsync();
        }

        private async Task SeedInventory(string sku, int actualQty, int reservedQty = 0)
        {
            /*using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

            // Gunakan factory method yang sudah menyediakan RowVersion
            var inventory = InventoryItem.CreateForTest(sku, actualQty, reservedQty);
            db.Inventory.Add(inventory);
            await db.SaveChangesAsync();*/
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

            // Pastikan RowVersion di-set dengan benar
            var inventory = InventoryItem.CreateForTest(sku, actualQty, reservedQty);

            // Jika CreateForTest tidak mengatur RowVersion, atur manual
            if (inventory.RowVersion == null || inventory.RowVersion.Length == 0)
            {
                // Gunakan reflection untuk set RowVersion
                var rowVersionProperty = typeof(InventoryItem)
                    .GetProperty("RowVersion",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Public);

                if (rowVersionProperty != null)
                {
                    rowVersionProperty.SetValue(inventory, new byte[8]);
                }
            }

            db.Inventory.Add(inventory);
            await db.SaveChangesAsync();
        }
    }
}