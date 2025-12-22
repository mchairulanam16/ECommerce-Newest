using ECommerce.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Repositories;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Application.Services
{
    public record PaymentResult(
        Guid OrderId,
        string PaymentExternalId,
        OrderStatus Status,
        bool IsIdempotent
    );
    public class OrderPaymentService
    {
        private readonly IOrderRepository _orderRepo;
        private readonly IInventoryRepository _inventoryRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<OrderPaymentService> _logger;

        public OrderPaymentService(
            IOrderRepository orderRepo,
            IInventoryRepository inventoryRepo,
            IUnitOfWork unitOfWork,
            ILogger<OrderPaymentService> logger)
        {
            _orderRepo = orderRepo;
            _inventoryRepo = inventoryRepo;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<PaymentResult> PayAsync(Guid orderId, string paymentExternalId)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var order = await _orderRepo.GetByIdWithItemsAsync(orderId);
                if (order == null)
                    throw new KeyNotFoundException("Order not found");

                bool isIdempotent = false;

                // Idempotent check
                if (order.Status == OrderStatus.PAID)
                {
                    if (order.PaymentExternalId == paymentExternalId)
                    {
                        _logger.LogInformation("Idempotent payment ignored OrderId={OrderId}", orderId);
                        isIdempotent = true;
                    }
                    else
                    {
                        throw new DomainException(
                            "Order already paid with different payment reference"
                        );
                    }
                }
                else
                {
                    // Validate payment reference
                    if (order.PaymentExternalId != paymentExternalId)
                        throw new DomainException("Invalid payment reference");

                    // Commit stock
                    foreach (var item in order.Items)
                    {
                        await _inventoryRepo.CommitAsync(item.Sku, item.Qty);
                    }

                    order.MarkPaid();
                    await _orderRepo.SaveChangesAsync();

                    _logger.LogInformation(
                        "EVENT OrderPaid OrderId={OrderId} PaymentExtId={ExtId}",
                        orderId,
                        paymentExternalId
                    );
                }

                return new PaymentResult(orderId, paymentExternalId, order.Status, isIdempotent);
            });
        }
    }
}
