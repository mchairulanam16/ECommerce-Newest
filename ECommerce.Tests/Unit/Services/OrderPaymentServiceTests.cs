using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;
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
    public class OrderPaymentServiceTests
    {
        private readonly Mock<IOrderRepository> _orderRepoMock;
        private readonly Mock<IInventoryRepository> _inventoryRepoMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<ILogger<OrderPaymentService>> _loggerMock;
        private readonly OrderPaymentService _sut;

        public OrderPaymentServiceTests()
        {
            _orderRepoMock = new Mock<IOrderRepository>();
            _inventoryRepoMock = new Mock<IInventoryRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _loggerMock = new Mock<ILogger<OrderPaymentService>>();

            _unitOfWorkMock
                .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<PaymentResult>>>()))
                .Returns<Func<Task<PaymentResult>>>(async func => await func());

            _sut = new OrderPaymentService(
                _orderRepoMock.Object,
                _inventoryRepoMock.Object,
                _unitOfWorkMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task PayAsync_WhenValidPayment_ShouldMarkOrderAsPaid()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var paymentExtId = "PAY-123456";

            var order = Order.Create(userId, new[] { ("A1", 2), ("B2", 3) });
            order.InitPayment(paymentExtId);

            _orderRepoMock
                .Setup(x => x.GetByIdWithItemsAsync(orderId))
                .ReturnsAsync(order);

            // Act
            var result = await _sut.PayAsync(orderId, paymentExtId);

            // Assert
            result.Should().NotBeNull();
            result.OrderId.Should().Be(orderId);
            result.PaymentExternalId.Should().Be(paymentExtId);
            result.Status.Should().Be(OrderStatus.PAID);
            result.IsIdempotent.Should().BeFalse();

            _inventoryRepoMock.Verify(x => x.CommitAsync("A1", 2), Times.Once);
            _inventoryRepoMock.Verify(x => x.CommitAsync("B2", 3), Times.Once);
            _orderRepoMock.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task PayAsync_WhenAlreadyPaidWithSameReference_ShouldBeIdempotent()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var paymentExtId = "PAY-123456";

            var order = Order.Create(userId, new[] { ("A1", 2) });
            order.InitPayment(paymentExtId);
            order.MarkPaid(); // Already paid

            _orderRepoMock
                .Setup(x => x.GetByIdWithItemsAsync(orderId))
                .ReturnsAsync(order);

            // Act
            var result = await _sut.PayAsync(orderId, paymentExtId);

            // Assert
            result.Status.Should().Be(OrderStatus.PAID);
            result.IsIdempotent.Should().BeTrue();

            // Should NOT commit inventory again
            _inventoryRepoMock.Verify(x => x.CommitAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
            _orderRepoMock.Verify(x => x.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task PayAsync_WhenAlreadyPaidWithDifferentReference_ShouldThrowException()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var correctPaymentExtId = "PAY-123456";
            var wrongPaymentExtId = "PAY-999999";

            var order = Order.Create(userId, new[] { ("A1", 2) });
            order.InitPayment(correctPaymentExtId);
            order.MarkPaid();

            _orderRepoMock
                .Setup(x => x.GetByIdWithItemsAsync(orderId))
                .ReturnsAsync(order);

            // Act
            Func<Task> act = async () => await _sut.PayAsync(orderId, wrongPaymentExtId);

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage("Order already paid with different payment reference");
        }

        [Fact]
        public async Task PayAsync_WhenInvalidPaymentReference_ShouldThrowException()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var correctPaymentExtId = "PAY-123456";
            var wrongPaymentExtId = "PAY-WRONG";

            var order = Order.Create(userId, new[] { ("A1", 2) });
            order.InitPayment(correctPaymentExtId);

            _orderRepoMock
                .Setup(x => x.GetByIdWithItemsAsync(orderId))
                .ReturnsAsync(order);

            // Act
            Func<Task> act = async () => await _sut.PayAsync(orderId, wrongPaymentExtId);

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage("Invalid payment reference");
        }

        [Fact]
        public async Task PayAsync_WhenOrderNotFound_ShouldThrowException()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var paymentExtId = "PAY-123456";

            _orderRepoMock
                .Setup(x => x.GetByIdWithItemsAsync(orderId))
                .ReturnsAsync((Order?)null);

            // Act
            Func<Task> act = async () => await _sut.PayAsync(orderId, paymentExtId);

            // Assert
            await act.Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage("Order not found");
        }
    }
}
