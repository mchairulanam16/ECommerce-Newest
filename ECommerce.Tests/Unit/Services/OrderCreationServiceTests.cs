
using ECommerce.Application.DTOs;
using ECommerce.Application.Services;
using ECommerce.Domain.Repositories;
using ECommerce.Domain.Entities;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Tests.Unit.Services
{
    public class OrderCreationServiceTests
    {
        private readonly Mock<IOrderRepository> _orderRepoMock;
        private readonly Mock<IInventoryRepository> _inventoryRepoMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<ILogger<OrderCreationService>> _loggerMock;
        private readonly OrderCreationService _sut;

        public OrderCreationServiceTests()
        {
            _orderRepoMock = new Mock<IOrderRepository>();
            _inventoryRepoMock = new Mock<IInventoryRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _loggerMock = new Mock<ILogger<OrderCreationService>>();

            // Setup UnitOfWork untuk langsung execute action
            _unitOfWorkMock
                .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<OrderResult>>>()))
                .Returns<Func<Task<OrderResult>>>(async func => await func());

            _sut = new OrderCreationService(
                _orderRepoMock.Object,
                _inventoryRepoMock.Object,
                _unitOfWorkMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task CreateAsync_WhenInventoryAvailable_ShouldCreateOrder()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var items = new List<CreateOrderItem>
            {
                new CreateOrderItem("A1", 2),
                new CreateOrderItem("B2", 3)
            };

            _inventoryRepoMock
                .Setup(x => x.TryReserveWithRetryAsync("A1", 2))
                .ReturnsAsync(true);

            _inventoryRepoMock
                .Setup(x => x.TryReserveWithRetryAsync("B2", 3))
                .ReturnsAsync(true);

            // Act
            var result = await _sut.CreateAsync(userId, items);

            // Assert
            result.OrderId.Should().NotBeEmpty();
            result.PaymentExternalId.Should().NotBeNullOrEmpty();
            result.PaymentExternalId.Should().StartWith("PAY-");

            _inventoryRepoMock.Verify(x => x.TryReserveWithRetryAsync("A1", 2), Times.Once);
            _inventoryRepoMock.Verify(x => x.TryReserveWithRetryAsync("B2", 3), Times.Once);
            _orderRepoMock.Verify(x => x.AddAsync(It.IsAny<Order>()), Times.Once);
            _orderRepoMock.Verify(x => x.SaveChangesAsync(), Times.Once);
            _unitOfWorkMock.Verify(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<OrderResult>>>()), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_WhenOutOfStock_ShouldThrowException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var items = new List<CreateOrderItem>
            {
                new("A1", 100)
            };

            _inventoryRepoMock
                .Setup(x => x.TryReserveWithRetryAsync("A1", 100))
                .ReturnsAsync(false);

            // Act
            Func<Task> act = async () => await _sut.CreateAsync(userId, items);

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage("Out of stock for A1");

            _orderRepoMock.Verify(x => x.AddAsync(It.IsAny<Order>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_WhenDuplicateSkuInItems_ShouldGroupAndReserveOnce()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var items = new List<CreateOrderItem>
            {
                new("A1", 2),
                new("A1", 3), // duplicate SKU
                new("A1", 1)  // another duplicate
            };

            _inventoryRepoMock
                .Setup(x => x.TryReserveWithRetryAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(true);

            // Act
            var result = await _sut.CreateAsync(userId, items);

            // Assert
            result.Should().NotBeNull();

            // FIX: Correct calculation: 2 + 3 + 1 = 6, not 5
            // Only one SKU (A1) should be reserved
            _inventoryRepoMock.Verify(x => x.TryReserveWithRetryAsync("A1", 6), Times.Once);

            // FIX: Remove the B2 verification since we don't have B2 in our items list
            _inventoryRepoMock.Verify(x => x.TryReserveWithRetryAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Exactly(1));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task CreateAsync_WhenInvalidQuantity_ShouldThrowException(int invalidQty)
        {
            // Arrange
            var userId = Guid.NewGuid();
            var items = new List<CreateOrderItem>
            {
                new("A1", invalidQty)
            };

            _inventoryRepoMock
                .Setup(x => x.TryReserveWithRetryAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(true);

            // Act
            Func<Task> act = async () => await _sut.CreateAsync(userId, items);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>();
        }
    }
}
