using Castle.Core.Logging;
using ECommerce.Application.DTOs;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Exceptions;
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

namespace ECommerce.Tests.Integrations
{
    public class IntegrationTests
    {
        private readonly Mock<IOrderRepository> mockOrderRepo;
        private readonly Mock<IInventoryRepository> mockInventoryRepo;
        private readonly Mock<IUnitOfWork> mockUnitOfWork;
        private readonly Mock<ILogger<OrderCreationService>> mockLogger;

        public IntegrationTests()
        {
            mockOrderRepo = new Mock<IOrderRepository>();
            mockInventoryRepo = new Mock<IInventoryRepository>();
            mockUnitOfWork = new Mock<IUnitOfWork>();
            mockLogger = new Mock<ILogger<OrderCreationService>>();

            SetupCommonMocks();
        }

        private void SetupCommonMocks()
        {
            // Setup transaction handling
            mockUnitOfWork
                .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<OrderResult>>>()))
                .Returns<Func<Task<OrderResult>>>(async func => await func());

            // Setup order repository
            mockOrderRepo
                .Setup(x => x.AddAsync(It.IsAny<Order>()))
                .Returns(Task.CompletedTask);

            mockOrderRepo
                .Setup(x => x.SaveChangesAsync())
                .Returns(Task.CompletedTask);
        }

        private OrderCreationService CreateService()
        {
            return new OrderCreationService(
                mockOrderRepo.Object,
                mockInventoryRepo.Object,
                mockUnitOfWork.Object,
                mockLogger.Object);
        }
        [Fact]
        public async Task FlashSale_HighConcurrency_ShouldNotExceedStock()
        {
            // Arrange
            const int stock = 100;
            const int concurrentRequests = 500;
            const string flashSku = "FLASHSALE-A1";

            var mockOrderRepo = new Mock<IOrderRepository>();
            var mockInventoryRepo = new Mock<IInventoryRepository>();
            var mockUnitOfWork = new Mock<IUnitOfWork>();
            var mockLogger = new Mock<ILogger<OrderCreationService>>();

            // Simulasi inventory dengan thread-safe counter
            var remainingStock = stock;
            var lockObject = new object();

            mockInventoryRepo
                .Setup(x => x.TryReserveWithRetryAsync(flashSku, 1))
                .ReturnsAsync(() =>
                {
                    lock (lockObject)
                    {
                        if (remainingStock > 0)
                        {
                            remainingStock--;
                            return true;
                        }
                        return false;
                    }
                });

            mockUnitOfWork
                .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<OrderResult>>>()))
                .Returns<Func<Task<OrderResult>>>(async func => await func());

            var service = new OrderCreationService(
                mockOrderRepo.Object,
                mockInventoryRepo.Object,
                mockUnitOfWork.Object,
                mockLogger.Object);

            var results = new ConcurrentBag<bool>();

            // Act
            var tasks = Enumerable.Range(0, concurrentRequests)
                .Select(async _ =>
                {
                    try
                    {
                        await service.CreateAsync(
                            Guid.NewGuid(),
                            new List<CreateOrderItem> { new(flashSku, 1) }
                        );
                        results.Add(true);
                    }
                    catch (DomainException)
                    {
                        results.Add(false);
                    }
                });

            await Task.WhenAll(tasks);

            // Assert
            var successCount = results.Count(r => r);
            successCount.Should().Be(stock); // Tidak boleh lebih dari stock
        }

        [Fact]
        public async Task MultipleSkus_ConcurrentRequests_ShouldHandleCorrectly()
        {
            // Arrange
            var inventory = new Dictionary<string, int>
            {
                ["A1"] = 50,
                ["B2"] = 30,
                ["C3"] = 20
            };

            const int requests = 150;

            var remainingStocks = new ConcurrentDictionary<string, int>(
                inventory.Select(kvp => new KeyValuePair<string, int>(kvp.Key, kvp.Value))
            );

            mockInventoryRepo
                .Setup(x => x.TryReserveAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync((string sku, int qty) =>
                {
                    lock (remainingStocks)
                    {
                        if (remainingStocks.TryGetValue(sku, out var remaining) && remaining >= qty)
                        {
                            remainingStocks[sku] = remaining - qty;
                            return true;
                        }
                        return false;
                    }
                });

            mockUnitOfWork
                .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<OrderResult>>>()))
                .Returns<Func<Task<OrderResult>>>(async func => await func());

            var service = new OrderCreationService(
                mockOrderRepo.Object,
                mockInventoryRepo.Object,
                mockUnitOfWork.Object,
                mockLogger.Object);

            var results = new ConcurrentBag<(string sku, bool success)>();

            // Act - Random SKU requests
            var random = new Random();
            var skus = inventory.Keys.ToList();

            var tasks = Enumerable.Range(0, requests)
                .Select(async _ =>
                {
                    var sku = skus[random.Next(skus.Count)];

                    try
                    {
                        await service.CreateAsync(
                            Guid.NewGuid(),
                            new List<CreateOrderItem> { new(sku, 1) }
                        );
                        results.Add((sku, true));
                    }
                    catch (DomainException)
                    {
                        results.Add((sku, false));
                    }
                });

            await Task.WhenAll(tasks);

            // Assert
            var successBySku = results
                .Where(r => r.success)
                .GroupBy(r => r.sku)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var sku in inventory.Keys)
            {
                var maxStock = inventory[sku];
                var actual = successBySku.GetValueOrDefault(sku, 0);
                actual.Should().BeLessThanOrEqualTo(maxStock);
            }
        }

        [Fact]
        public async Task SameUser_MultipleConcurrentOrders_ShouldBeHandled()
        {
            // Arrange
            const int stock = 10;
            const string sku = "LIMITED-SKU";
            var userId = Guid.NewGuid();

            var remainingStock = stock;
            var lockObject = new object();

            mockInventoryRepo
                .Setup(x => x.TryReserveAsync(sku, 1))
                .ReturnsAsync(() =>
                {
                    lock (lockObject)
                    {
                        if (remainingStock > 0)
                        {
                            remainingStock--;
                            return true;
                        }
                        return false;
                    }
                });

            mockUnitOfWork
                .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<OrderResult>>>()))
                .Returns<Func<Task<OrderResult>>>(async func => await func());

            var service = new OrderCreationService(
                mockOrderRepo.Object,
                mockInventoryRepo.Object,
                mockUnitOfWork.Object,
                mockLogger.Object);

            var results = new ConcurrentBag<bool>();

            // Act - User yang sama mencoba order berkali-kali
            var tasks = Enumerable.Range(0, 20)
                .Select(async _ =>
                {
                    try
                    {
                        await service.CreateAsync(
                            userId, // SAME USER
                            new List<CreateOrderItem> { new(sku, 1) }
                        );
                        results.Add(true);
                    }
                    catch (DomainException)
                    {
                        results.Add(false);
                    }
                });

            await Task.WhenAll(tasks);

            // Assert
            var successCount = results.Count(r => r);
            successCount.Should().BeLessThanOrEqualTo(stock);
        }
    }
}