using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Tests.Unit.Domain
{
    public class OrderTests
    {
        [Fact]
        public void Create_WhenValidItems_ShouldCreateOrder()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var items = new[] { ("A1", 2), ("B2", 3) };

            // Act
            var order = Order.Create(userId, items);

            // Assert
            order.Should().NotBeNull();
            order.Id.Should().NotBeEmpty();
            order.UserId.Should().Be(userId);
            order.Status.Should().Be(OrderStatus.PLACED);
            order.Items.Should().HaveCount(2);
            order.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void Create_WhenNoItems_ShouldThrowException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var items = Array.Empty<(string sku, int qty)>();

            // Act
            Action act = () => Order.Create(userId, items);

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("Order must have items");
        }

        [Fact]
        public void MarkPaid_WhenOrderIsPlaced_ShouldUpdateStatus()
        {
            // Arrange
            var order = Order.Create(Guid.NewGuid(), new[] { ("A1", 2) });
            order.InitPayment("PAY-123");

            // Act
            order.MarkPaid();

            // Assert
            order.Status.Should().Be(OrderStatus.PAID);
        }

        [Fact]
        public void MarkPaid_WhenOrderAlreadyPaid_ShouldThrowException()
        {
            // Arrange
            var order = Order.Create(Guid.NewGuid(), new[] { ("A1", 2) });
            order.InitPayment("PAY-123");
            order.MarkPaid();

            // Act
            Action act = () => order.MarkPaid();

            // Assert
            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void Cancel_WhenOrderIsPlaced_ShouldUpdateStatus()
        {
            // Arrange
            var order = Order.Create(Guid.NewGuid(), new[] { ("A1", 2) });

            // Act
            order.Cancel();

            // Assert
            order.Status.Should().Be(OrderStatus.CANCELLED);
        }

        [Fact]
        public void Cancel_WhenOrderIsShipped_ShouldThrowException()
        {
            // Arrange
            var order = Order.Create(Guid.NewGuid(), new[] { ("A1", 2) });
            order.InitPayment("PAY-123");
            order.MarkPaid();
            order.MarkShipped();

            // Act
            Action act = () => order.Cancel();

            // Assert
            act.Should().Throw<DomainException>()
                .WithMessage("Cannot cancel order in status SHIPPED");
        }
    }
}
