using Castle.Core.Logging;
using ECommerce.Application.DTOs;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ECommerce.Tests.LoadTests
{
    public class FlashSaleLoadTest
    {
        [Fact]
        public async Task FlashSale_100ConcurrentOrders_ShouldNotExceedStock()
        {
            // Arrange
            const int stock = 10;           // Hanya 10 stok tersedia
            const int concurrentOrders = 100; // 100 user mencoba order bersamaan
            const string flashSaleSku = "FLASH-001";

            // Mock repositories
            var mockOrderRepo = new Mock<IOrderRepository>();
            var mockInventoryRepo = new Mock<IInventoryRepository>();
            var mockUnitOfWork = new Mock<IUnitOfWork>();
            var mockLogger = new Mock<ILogger<OrderCreationService>>();

            // Simulasi stok dengan thread-safe counter
            var remainingStock = stock;
            var lockObject = new object();

            // Setup TryReserveAsync untuk cek dan kurangi stok
            mockInventoryRepo
                .Setup(x => x.TryReserveAsync(flashSaleSku, 1))
                .ReturnsAsync(() =>
                {
                    lock (lockObject)  // Gunakan lock untuk thread safety
                    {
                        if (remainingStock > 0)
                        {
                            remainingStock--;
                            return true;
                        }
                        return false;
                    }
                });

            // Setup UnitOfWork untuk transaction
            mockUnitOfWork
                .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<OrderResult>>>()))
                .Returns<Func<Task<OrderResult>>>(async func => await func());

            // Setup mock order repo untuk simulasikan save order
            mockOrderRepo
                .Setup(x => x.AddAsync(It.IsAny<Order>()))
                .Returns(Task.CompletedTask);

            mockOrderRepo
                .Setup(x => x.SaveChangesAsync())
                .Returns(Task.CompletedTask);

            // Create service
            var service = new OrderCreationService(
                mockOrderRepo.Object,
                mockInventoryRepo.Object,
                mockUnitOfWork.Object,
                mockLogger.Object);

            var results = new ConcurrentBag<(bool success, Guid orderId)>();

            // Act - Simulasi 100 concurrent orders
            var tasks = Enumerable.Range(0, concurrentOrders)
                .Select(async i =>
                {
                    try
                    {
                        var result = await service.CreateAsync(
                            Guid.NewGuid(),  // User ID unik untuk setiap request
                            new List<CreateOrderItem>
                            {
                                new CreateOrderItem(flashSaleSku, 1)
                            }
                        );

                        results.Add((true, result.OrderId));
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("Out of stock"))
                    {
                        results.Add((false, Guid.Empty));
                    }
                });

            await Task.WhenAll(tasks);

            // Assert
            var successCount = results.Count(r => r.success);
            var failCount = results.Count(r => !r.success);

            // Hanya 10 yang harus berhasil (sesuai stok)
            successCount.Should().Be(stock);
            failCount.Should().Be(concurrentOrders - stock);

            // Log untuk debugging
            Console.WriteLine($"=== Flash Sale Test Results ===");
            Console.WriteLine($"Stock Available: {stock}");
            Console.WriteLine($"Concurrent Orders: {concurrentOrders}");
            Console.WriteLine($"Successful Orders: {successCount}");
            Console.WriteLine($"Failed Orders: {failCount}");
            Console.WriteLine($"===============================");
        }

        [Fact]
        public async Task FlashSale_WithVaryingQuantities_ShouldHandleCorrectly()
        {
            // Arrange
            const int stock = 100;
            const int requests = 80;
            const string sku = "FLASH-002";

            var mockOrderRepo = new Mock<IOrderRepository>();
            var mockInventoryRepo = new Mock<IInventoryRepository>();
            var mockUnitOfWork = new Mock<IUnitOfWork>();
            var mockLogger = new Mock<ILogger<OrderCreationService>>();

            var remainingStock = stock;
            var lockObject = new object();

            mockInventoryRepo
                .Setup(x => x.TryReserveAsync(sku, It.IsAny<int>()))
                .ReturnsAsync((string _, int qty) =>
                {
                    lock (lockObject)
                    {
                        if (remainingStock >= qty)
                        {
                            remainingStock -= qty;
                            return true;
                        }
                        return false;
                    }
                });

            mockUnitOfWork
                .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<OrderResult>>>()))
                .Returns<Func<Task<OrderResult>>>(async func => await func());

            mockOrderRepo
                .Setup(x => x.AddAsync(It.IsAny<Order>()))
                .Returns(Task.CompletedTask);

            mockOrderRepo
                .Setup(x => x.SaveChangesAsync())
                .Returns(Task.CompletedTask);

            var service = new OrderCreationService(
                mockOrderRepo.Object,
                mockInventoryRepo.Object,
                mockUnitOfWork.Object,
                mockLogger.Object);

            var results = new ConcurrentBag<(bool success, int qty)>();

            // Act - Simulasikan order dengan quantity berbeda (1, 2, atau 3)
            var tasks = Enumerable.Range(0, requests)
                .Select(async i =>
                {
                    var qty = (i % 3) + 1; // 1, 2, atau 3

                    try
                    {
                        var result = await service.CreateAsync(
                            Guid.NewGuid(),
                            new List<CreateOrderItem>
                            {
                                new CreateOrderItem(sku, qty)
                            }
                        );

                        results.Add((true, qty));
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("Out of stock"))
                    {
                        results.Add((false, qty));
                    }
                });

            await Task.WhenAll(tasks);

            // Assert
            var totalReserved = results
                .Where(r => r.success)
                .Sum(r => r.qty);

            totalReserved.Should().BeLessThanOrEqualTo(stock);

            Console.WriteLine($"=== Varying Quantities Test ===");
            Console.WriteLine($"Stock: {stock}");
            Console.WriteLine($"Requests: {requests}");
            Console.WriteLine($"Total Reserved: {totalReserved}");
            Console.WriteLine($"Successful Orders: {results.Count(r => r.success)}");
            Console.WriteLine($"===============================");
        }

        [Theory]
        [InlineData(50, 50, 1)]     // Exact match
        [InlineData(50, 100, 1)]    // More requests than stock
        [InlineData(50, 25, 1)]     // Less requests than stock
        [InlineData(0, 100, 1)]     // Zero stock
        [InlineData(100, 40, 2)]    // 2 units per order
        public async Task FlashSale_VariousScenarios_ShouldBeCorrect(
            int stock, int requests, int qtyPerOrder)
        {
            // Arrange
            const string sku = "FLASH-TEST";

            var mockOrderRepo = new Mock<IOrderRepository>();
            var mockInventoryRepo = new Mock<IInventoryRepository>();
            var mockUnitOfWork = new Mock<IUnitOfWork>();
            var mockLogger = new Mock<ILogger<OrderCreationService>>();

            var remainingStock = stock;
            var lockObject = new object();

            mockInventoryRepo
                .Setup(x => x.TryReserveAsync(sku, It.IsAny<int>()))
                .ReturnsAsync((string _, int qty) =>
                {
                    lock (lockObject)
                    {
                        if (remainingStock >= qty)
                        {
                            remainingStock -= qty;
                            return true;
                        }
                        return false;
                    }
                });

            mockUnitOfWork
                .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<OrderResult>>>()))
                .Returns<Func<Task<OrderResult>>>(async func => await func());

            mockOrderRepo
                .Setup(x => x.AddAsync(It.IsAny<Order>()))
                .Returns(Task.CompletedTask);

            mockOrderRepo
                .Setup(x => x.SaveChangesAsync())
                .Returns(Task.CompletedTask);

            var service = new OrderCreationService(
                mockOrderRepo.Object,
                mockInventoryRepo.Object,
                mockUnitOfWork.Object, 
                mockLogger.Object);

            var successCount = 0;

            // Act
            var tasks = Enumerable.Range(0, requests)
                .Select(async _ =>
                {
                    try
                    {
                        await service.CreateAsync(
                            Guid.NewGuid(),
                            new List<CreateOrderItem>
                            {
                                new CreateOrderItem(sku, qtyPerOrder)
                            }
                        );

                        Interlocked.Increment(ref successCount);
                    }
                    catch (InvalidOperationException)
                    {
                        // Expected failure
                    }
                });

            await Task.WhenAll(tasks);

            // Assert
            var expectedMaxSuccess = stock == 0 ? 0 : stock / qtyPerOrder;
            var expectedSuccess = Math.Min(expectedMaxSuccess, requests);

            successCount.Should().Be(expectedSuccess);

            Console.WriteLine($"Scenario: Stock={stock}, Requests={requests}, Qty/Order={qtyPerOrder}");
            Console.WriteLine($"Expected Success: {expectedSuccess}, Actual: {successCount}");
        }
    }
}