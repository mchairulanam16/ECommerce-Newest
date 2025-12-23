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

        [Fact]
        public async Task FlashSale_100ConcurrentRequests_ShouldNotOverReserve()
        {
            // Arrange
            await ResetDatabase();
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
                });

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

        // Helper methods
        private async Task ResetDatabase()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
        }

        private async Task SeedInventory(string sku, int actualQty, int reservedQty)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

            var inventory = new InventoryItem(sku, actualQty);
            db.Inventory.Add(inventory);
            await db.SaveChangesAsync();

            // If reservedQty > 0, we need to simulate reservation
            if (reservedQty > 0)
            {
                // This depends on your InventoryItem implementation
                // You might need to call a Reserve method or use reflection
            }
        }
    }
}