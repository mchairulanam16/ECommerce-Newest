using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ECommerce.Domain.Exceptions;
using ECommerce.Domain.Repositories;

namespace ECommerce.Application.Services
{
    public class OrderShippedService
    {
        private readonly IOrderRepository _orderRepo;
        private readonly ILogger<OrderShippedService> _logger;

        public OrderShippedService(
            IOrderRepository orderRepo,
            ILogger<OrderShippedService> logger)
        {
            _orderRepo = orderRepo;
            _logger = logger;
        }

        public async Task ShippedAsync(Guid orderId)
        {
            var order = await _orderRepo.GetByIdAsync(orderId);
            if (order == null)
                throw new KeyNotFoundException("Order not found");

            if (order.Status != OrderStatus.PAID)
                throw new DomainException("Only paid orders can be shipped");

            order.MarkShipped();
            await _orderRepo.SaveChangesAsync();

            _logger.LogInformation("EVENT OrderShipped {OrderId}", orderId);
        }
    }

}
