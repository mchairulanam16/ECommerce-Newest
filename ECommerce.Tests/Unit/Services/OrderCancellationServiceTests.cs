using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Tests.Unit.Services
{
    public class OrderCancellationServiceTests
    {
        private readonly Mock<IOrderRepository> _orderRepoMock;
        private readonly Mock<IInventoryRepository> _inventoryRepoMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<ILogger<OrderCancellationService>> _loggerMock;
        private readonly OrderCancellationService _sut;

        public OrderCancellationServiceTests()
        {
            _orderRepoMock = new Mock<IOrderRepository>();
            _inventoryRepoMock = new Mock<IInventoryRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _loggerMock = new Mock<ILogger<OrderCancellationService>>();

            _unitOfWorkMock
                .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
                .Returns<Func<Task>>(async func => await func());

            _sut = new OrderCancellationService(
                _orderRepoMock.Object,
                _inventoryRepoMock.Object,
                _unitOfWorkMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task CancelAsync_WhenOrderIsPlaced_ShouldCancelAndReleaseInventory()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var order = Order.Create(userId, new[] { ("A1", 2), ("B2", 3) });

            _orderRepoMock
                .Setup(x => x.GetByIdWithItemsAsync(orderId))
                .ReturnsAsync(order);

            // Act
            await _sut.CancelAsync(orderId);

            // Assert
            order.Status.Should().Be(OrderStatus.CANCELLED);

            _inventoryRepoMock.Verify(x => x.ReleaseAsync("A1", 2), Times.Once);
            _inventoryRepoMock.Verify(x => x.ReleaseAsync("B2", 3), Times.Once);
            _orderRepoMock.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task CancelAsync_WhenOrderIsPaid_ShouldThrowException()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var order = Order.Create(userId, new[] { ("A1", 2) });
            order.InitPayment("PAY-123");
            order.MarkPaid();

            _orderRepoMock
                .Setup(x => x.GetByIdWithItemsAsync(orderId))
                .ReturnsAsync(order);

            // Act
            Func<Task> act = async () => await _sut.CancelAsync(orderId);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Paid order need verification to be cancelled");

            _inventoryRepoMock.Verify(x => x.ReleaseAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task CancelAsync_WhenOrderNotFound_ShouldThrowException()
        {
            // Arrange
            var orderId = Guid.NewGuid();

            _orderRepoMock
                .Setup(x => x.GetByIdWithItemsAsync(orderId))
                .ReturnsAsync((Order?)null);

            // Act
            Func<Task> act = async () => await _sut.CancelAsync(orderId);

            // Assert
            await act.Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage("Order not found");
        }
    }
}
