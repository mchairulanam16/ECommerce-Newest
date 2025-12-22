using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ECommerce.Domain.Repositories;

namespace ECommerce.Application.Services
{
    public class OrderCancellationService
    {
        private readonly IOrderRepository _orderRepo;
        private readonly IInventoryRepository _inventoryRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<OrderCancellationService> _logger;

        public OrderCancellationService(
            IOrderRepository orderRepo,
            IInventoryRepository inventoryRepo,
            IUnitOfWork unitOfWork,
            ILogger<OrderCancellationService> logger)
        {
            _orderRepo = orderRepo;
            _inventoryRepo = inventoryRepo;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task CancelAsync(Guid orderId)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var order = await _orderRepo.GetByIdWithItemsAsync(orderId);
                if (order == null)
                    throw new KeyNotFoundException("Order not found");

                if (order.Status == OrderStatus.PAID)
                    throw new InvalidOperationException("Paid order need verification to be cancelled");

                // Release inventory
                foreach (var item in order.Items)
                {
                    await _inventoryRepo.ReleaseAsync(item.Sku, item.Qty);
                }

                order.Cancel();
                await _orderRepo.SaveChangesAsync();

                _logger.LogInformation("EVENT OrderCancelled {OrderId}", orderId);
            });
        }
    }

}
