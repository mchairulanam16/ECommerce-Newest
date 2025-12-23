using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ECommerce.Application.DTOs;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Integration
{
    public class ApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory _factory;
        private readonly ECommerceDbContext _dbContext;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();

            // Get DbContext for setup/verification
            var scope = factory.Services.CreateScope();
            _dbContext = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public void Dispose()
        {
            // Cleanup after each test
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
        }

        // ===== TEST CASE 1 =====
        [Fact]
        public async Task CreateOrder_SingleItem_SufficientStock_ShouldSucceed()
        {
            // Arrange
            await ResetDatabase();
            await SeedInventory("A1", actualQty: 10, reservedQty: 0);

            var request = new
            {
                userId = Guid.NewGuid(),
                items = new[]
                {
                    new { sku = "A1", qty = 2 }
                }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/orders", request);

            // Assert - Response
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var orderResponse = await response.Content.ReadFromJsonAsync<OrderResult>(_jsonOptions);
            orderResponse.Should().NotBeNull();
            orderResponse.OrderId.Should().NotBeEmpty();
            orderResponse.PaymentExternalId.Should().NotBeNullOrEmpty();
            orderResponse.PaymentExternalId.Should().StartWith("PAY-");

            // Assert - Database state
            var order = await _dbContext.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderResponse.OrderId);

            order.Should().NotBeNull();
            order.Status.Should().Be(OrderStatus.PLACED);
            order.Items.Should().HaveCount(1);
            order.Items.First().Sku.Should().Be("A1");
            order.Items.First().Qty.Should().Be(2);

            // Assert - Inventory updated
            var inventory = await _dbContext.Inventory
                .FirstOrDefaultAsync(i => i.Sku == "A1");

            inventory.Should().NotBeNull();
            inventory.ActualQty.Should().Be(10);     // Unchanged
            inventory.ReservedQty.Should().Be(2);    // Reserved
        }

        // ===== TEST CASE 2 =====
        [Fact]
        public async Task CreateOrder_MultiItem_OneOutOfStock_ShouldFailWithRollback()
        {
            // Arrange
            await ResetDatabase();
            await SeedInventory("A1", actualQty: 5, reservedQty: 0);
            await SeedInventory("B2", actualQty: 10, reservedQty: 0);

            var request = new
            {
                userId = Guid.NewGuid(),
                items = new[]
                {
                    new { sku = "A1", qty = 3 },   // OK: 5 available
                    new { sku = "B2", qty = 20 }   // FAIL: Only 10 available
                }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/orders", request);

            // Assert - Response
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var error = await response.Content.ReadAsStringAsync();
            error.Should().Contain("Out of stock");
            error.Should().Contain("B2");

            // Assert - No order created
            var ordersCount = await _dbContext.Orders.CountAsync();
            ordersCount.Should().Be(0);

            // Assert - Inventory unchanged (rollback)
            var inventoryA1 = await _dbContext.Inventory
                .FirstOrDefaultAsync(i => i.Sku == "A1");
            var inventoryB2 = await _dbContext.Inventory
                .FirstOrDefaultAsync(i => i.Sku == "B2");

            inventoryA1.ReservedQty.Should().Be(0); // No reservation
            inventoryB2.ReservedQty.Should().Be(0); // No reservation
        }

        // ===== TEST CASE 3 =====
        [Fact]
        public async Task ProcessPayment_WithSamePaymentId_ShouldBeIdempotent()
        {
            // Arrange
            await ResetDatabase();
            await SeedInventory("A1", actualQty: 10, reservedQty: 0);

            // 1. Create order
            var createRequest = new
            {
                userId = Guid.NewGuid(),
                items = new[] { new { sku = "A1", qty = 1 } }
            };

            var createResponse = await _client.PostAsJsonAsync("/api/orders", createRequest);
            createResponse.EnsureSuccessStatusCode();

            var order = await createResponse.Content.ReadFromJsonAsync<OrderResult>(_jsonOptions);

            // 2. First payment attempt
            var paymentId = $"PAY-{Guid.NewGuid():N}";
            var paymentRequest = new { paymentExternalId = paymentId };

            var firstPaymentResponse = await _client.PostAsJsonAsync(
                $"/api/orders/{order.OrderId}/pay",
                paymentRequest);

            // Assert - First payment succeeds
            firstPaymentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var firstPaymentResult = await firstPaymentResponse.Content.ReadFromJsonAsync<OrderResult>(_jsonOptions);
            firstPaymentResult.Status.Should().Be("PAID");

            // Get initial state
            var orderBefore = await _dbContext.Orders.FindAsync(order.OrderId);
            var inventoryBefore = await _dbContext.Inventory.FirstAsync(i => i.Sku == "A1");

            // 3. Second payment attempt with SAME payment ID
            var secondPaymentResponse = await _client.PostAsJsonAsync(
                $"/api/orders/{order.OrderId}/pay",
                paymentRequest);

            // Assert - Second payment is idempotent
            secondPaymentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var secondPaymentResult = await secondPaymentResponse.Content.ReadFromJsonAsync<OrderResult>(_jsonOptions);
            secondPaymentResult.Status.Should().Be("PAID"); // Same status

            // Verify no duplicate changes
            var orderAfter = await _dbContext.Orders.FindAsync(order.OrderId);
            var inventoryAfter = await _dbContext.Inventory.FirstAsync(i => i.Sku == "A1");

            // Should be exactly the same
            orderAfter.Status.Should().Be(orderBefore.Status);
            orderAfter.PaymentExternalId.Should().Be(orderBefore.PaymentExternalId);
            inventoryAfter.ActualQty.Should().Be(inventoryBefore.ActualQty);
            inventoryAfter.ReservedQty.Should().Be(inventoryBefore.ReservedQty);
        }

        // ===== TEST CASE 4 =====
        [Fact]
        public async Task CancelOrder_BeforePayment_ShouldReleaseInventory()
        {
            // Arrange
            await ResetDatabase();
            await SeedInventory("A1", actualQty: 10, reservedQty: 0);

            // 1. Create order (reserves inventory)
            var createRequest = new
            {
                userId = Guid.NewGuid(),
                items = new[] { new { sku = "A1", qty = 2 } }
            };

            var createResponse = await _client.PostAsJsonAsync("/api/orders", createRequest);
            createResponse.EnsureSuccessStatusCode();

            var order = await createResponse.Content.ReadFromJsonAsync<OrderResult>(_jsonOptions);

            // Verify inventory reserved
            var inventoryBefore = await _dbContext.Inventory.FirstAsync(i => i.Sku == "A1");
            inventoryBefore.ReservedQty.Should().Be(2);

            // Act - Cancel order
            var cancelResponse = await _client.PostAsync(
                $"/api/orders/{order.OrderId}/cancel",
                null);

            // Assert
            cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var cancelResult = await cancelResponse.Content.ReadFromJsonAsync<OrderResult>(_jsonOptions);
            cancelResult.Status.Should().Be("CANCELLED");

            // Verify inventory released
            var inventoryAfter = await _dbContext.Inventory.FirstAsync(i => i.Sku == "A1");
            inventoryAfter.ReservedQty.Should().Be(0); // Released
            inventoryAfter.ActualQty.Should().Be(10);  // Unchanged

            // Verify order status
            var dbOrder = await _dbContext.Orders.FindAsync(order.OrderId);
            dbOrder.Status.Should().Be(OrderStatus.CANCELLED);
        }

        // ===== TEST CASE 5 =====
        [Fact]
        public async Task CancelOrder_AfterPayment_ShouldReturnConflict()
        {
            // Arrange
            await ResetDatabase();
            await SeedInventory("A1", actualQty: 10, reservedQty: 0);

            // 1. Create and pay for order
            var createRequest = new
            {
                userId = Guid.NewGuid(),
                items = new[] { new { sku = "A1", qty = 2 } }
            };

            var createResponse = await _client.PostAsJsonAsync("/api/orders", createRequest);
            createResponse.EnsureSuccessStatusCode();

            var order = await createResponse.Content.ReadFromJsonAsync<OrderResult>(_jsonOptions);

            // 2. Process payment
            var paymentRequest = new { paymentExternalId = $"PAY-{Guid.NewGuid():N}" };
            var paymentResponse = await _client.PostAsJsonAsync(
                $"/api/orders/{order.OrderId}/pay",
                paymentRequest);
            paymentResponse.EnsureSuccessStatusCode();

            // Verify order is PAID and inventory committed
            var paidOrder = await _dbContext.Orders.FindAsync(order.OrderId);
            paidOrder.Status.Should().Be(OrderStatus.PAID);

            var inventoryAfterPayment = await _dbContext.Inventory.FirstAsync(i => i.Sku == "A1");
            inventoryAfterPayment.ActualQty.Should().Be(8);  // 10 - 2 = 8
            inventoryAfterPayment.ReservedQty.Should().Be(0); // Released after payment

            // Act - Try to cancel paid order
            var cancelResponse = await _client.PostAsync(
                $"/api/orders/{order.OrderId}/cancel",
                null);

            // Assert
            cancelResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

            var error = await cancelResponse.Content.ReadAsStringAsync();
            error.Should().Contain("Paid order");

            // Verify no changes
            var orderAfterCancel = await _dbContext.Orders.FindAsync(order.OrderId);
            orderAfterCancel.Status.Should().Be(OrderStatus.PAID); // Still PAID

            var inventoryAfterCancel = await _dbContext.Inventory.FirstAsync(i => i.Sku == "A1");
            inventoryAfterCancel.ActualQty.Should().Be(8); // No restock
        }

        // ===== TEST CASE 7 =====
        [Fact]
        public async Task GetInventory_ShouldReturnCorrectQuantities()
        {
            // Arrange
            await ResetDatabase();
            await SeedInventory("A1", actualQty: 100, reservedQty: 25);
            await SeedInventory("B2", actualQty: 50, reservedQty: 10);

            // Act
            var response = await _client.GetAsync("/api/inventory/A1");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var inventory = await response.Content.ReadFromJsonAsync<InventoryResponse>(_jsonOptions);
            inventory.Should().NotBeNull();
            inventory.Sku.Should().Be("A1");
            inventory.ActualQty.Should().Be(100);
            inventory.ReservedQty.Should().Be(25);
            inventory.AvailableQty.Should().Be(75); // 100 - 25
        }

        // ===== HELPER METHODS =====
        private async Task ResetDatabase()
        {
            await _dbContext.Database.EnsureDeletedAsync();
            await _dbContext.Database.EnsureCreatedAsync();
        }

        // Dalam ApiIntegrationTests.cs
        private async Task SeedInventory(string sku, int actualQty, int reservedQty)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

            // Clear existing
            var existing = await db.Inventory
                .FirstOrDefaultAsync(i => i.Sku == sku);

            if (existing != null)
            {
                db.Inventory.Remove(existing);
                await db.SaveChangesAsync();
            }

            // ⭐ PERBAIKAN: Create inventory dengan RowVersion
            // Pakai reflection untuk set private properties jika perlu
            var inventory = new InventoryItem(sku, actualQty);

            // Jika perlu set ReservedQty, pakai reflection atau panggil Reserve()
            if (reservedQty > 0)
            {
                // Panggil Reserve method
                inventory.Reserve(reservedQty);
            }

            // Set RowVersion untuk in-memory (bisa null untuk test)
            var rowVersionProp = typeof(InventoryItem).GetProperty("RowVersion");
            if (rowVersionProp != null && rowVersionProp.CanWrite)
            {
                rowVersionProp.SetValue(inventory, new byte[8]);
            }

            db.Inventory.Add(inventory);
            await db.SaveChangesAsync();
        }
    }

    // Response DTOs
    public class OrderResult
    {
        public Guid OrderId { get; set; }
        public string PaymentExternalId { get; set; }
        public string Status { get; set; }
    }

    public class InventoryResponse
    {
        public string Sku { get; set; }
        public int ActualQty { get; set; }
        public int ReservedQty { get; set; }
        public int AvailableQty { get; set; }
    }
}