using ECommerce.Application.DTOs;
using ECommerce.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Domain.Repositories;
using Microsoft.Extensions.Logging;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Application.Services
{
    public record OrderResult(Guid OrderId, string PaymentExternalId);
    public class OrderCreationService
    {
        private readonly IOrderRepository _orderRepo;
        private readonly IInventoryRepository _inventoryRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<OrderCreationService> _logger;

        public OrderCreationService(
            IOrderRepository orderRepo,
            IInventoryRepository inventoryRepo,
            IUnitOfWork unitOfWork,
            ILogger<OrderCreationService> logger
            )
        {
            _orderRepo = orderRepo;
            _inventoryRepo = inventoryRepo;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<OrderResult> CreateAsync(Guid userId, List<CreateOrderItem> items)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var groupedItems = items
                    .GroupBy(x => x.Sku)
                    .Select(g => new { Sku = g.Key, Qty = g.Sum(x => x.Qty) })
                    .ToList();

                foreach (var item in groupedItems)
                {
                    var reserved = await _inventoryRepo.TryReserveWithRetryAsync(item.Sku, item.Qty);
                    if (!reserved)
                        throw new DomainException($"Out of stock for {item.Sku}");
                }

                var order = Order.Create(
                    userId,
                    items.Select(x => (x.Sku, x.Qty))
                );

                order.InitPayment($"PAY-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}");

                await _orderRepo.AddAsync(order);
                await _orderRepo.SaveChangesAsync();

                _logger.LogInformation("EVENT OrderPlaced {OrderId}", order.Id);

                return new OrderResult(order.Id, order.PaymentExternalId);
            });

        }

        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3)
        {
            int retryCount = 0;
            TimeSpan delay = TimeSpan.FromMilliseconds(100);

            while (true)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex) when (retryCount < maxRetries && IsTransientError(ex))
                {
                    retryCount++;
                    _logger.LogWarning(ex,
                        "Retry attempt {RetryCount} after {Delay}ms",
                        retryCount, delay.TotalMilliseconds);

                    await Task.Delay(delay);
                    delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // Exponential backoff
                }
            }
        }

        private bool IsTransientError(Exception ex)
        {
            return ex.Message.Contains("concurrent", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("RowVersion", StringComparison.OrdinalIgnoreCase);
        }
    }
}
