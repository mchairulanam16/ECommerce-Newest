using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Domain.Entities
{
    public class Order
    {
        public Guid Id { get; private set; }

        public Guid UserId { get; private set; }
        public OrderStatus Status { get; private set; }
        public string PaymentExternalId { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public List<OrderItem> Items { get; private set; } = new();

        public byte[] RowVersion { get; private set; } = default!;

        private Order() { }

        private Order(Guid userId, IEnumerable<(string sku, int qty)> items)
        {
            if (!items.Any())
                throw new ArgumentException("Order must have items");

            Id = Guid.NewGuid();
            UserId = userId;
            CreatedAt = DateTime.UtcNow;
            Status = OrderStatus.PLACED;

            Items = items
                .GroupBy(x => x.sku)
                .Select(g => new OrderItem(
                    Id,
                    g.Key,
                    g.Sum(x => x.qty)
                ))
                .ToList();
        }

        public static Order Create(Guid userId, IEnumerable<(string sku, int qty)> items)
        {
            return new Order(userId, items);
        }

        public void InitPayment(string externalId)
        {
            if (PaymentExternalId != null)
                throw new DomainException("Payment already initialized");

            PaymentExternalId = externalId;
        }

        public void MarkPaid()
        {
            if (Status != OrderStatus.PLACED)
                throw new DomainException($"Cannot pay order in status {Status}");

            Status = OrderStatus.PAID;
        }

        public void MarkShipped()
        {
            if (Status != OrderStatus.PAID)
                throw new DomainException($"Cannot pay order in status {Status}");

            Status = OrderStatus.SHIPPED;
        }

        public void Cancel()
        {
            if (Status == OrderStatus.SHIPPED)
                throw new DomainException($"Cannot cancel order in status {Status}");

            Status = OrderStatus.CANCELLED;
        }
    }

}
