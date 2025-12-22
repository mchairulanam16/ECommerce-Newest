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
    public class OrderShippedServiceTests
    {
        private readonly Mock<IOrderRepository> _orderRepoMock;
        private readonly Mock<ILogger<OrderShippedService>> _loggerMock;
        private readonly OrderShippedService _sut;

        public OrderShippedServiceTests()
        {
            _orderRepoMock = new Mock<IOrderRepository>();
            _loggerMock = new Mock<ILogger<OrderShippedService>>();

            _sut = new OrderShippedService(
                _orderRepoMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task ShippedAsync_WhenOrderIsPaid_ShouldMarkAsShipped()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var order = Order.Create(userId, new[] { ("A1", 2) });
            order.InitPayment("PAY-123");
            order.MarkPaid();

            _orderRepoMock
                .Setup(x => x.GetByIdAsync(orderId))
                .ReturnsAsync(order);

            // Act
            await _sut.ShippedAsync(orderId);

            // Assert
            order.Status.Should().Be(OrderStatus.SHIPPED);
            _orderRepoMock.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task ShippedAsync_WhenOrderNotPaid_ShouldThrowException()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var order = Order.Create(userId, new[] { ("A1", 2) });

            _orderRepoMock
                .Setup(x => x.GetByIdAsync(orderId))
                .ReturnsAsync(order);

            // Act
            Func<Task> act = async () => await _sut.ShippedAsync(orderId);

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage("Only paid orders can be shipped");
        }

        [Fact]
        public async Task ShippedAsync_WhenOrderNotFound_ShouldThrowException()
        {
            // Arrange
            var orderId = Guid.NewGuid();

            _orderRepoMock
                .Setup(x => x.GetByIdAsync(orderId))
                .ReturnsAsync((Order?)null);

            // Act
            Func<Task> act = async () => await _sut.ShippedAsync(orderId);

            // Assert
            await act.Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage("Order not found");
        }
    }
}
