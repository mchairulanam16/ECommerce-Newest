using ECommerce.Api.Contracts;
using ECommerce.Application.DTOs;
using ECommerce.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers
{
    [ApiController]
    [Route("orders")]
    public class OrdersController : ControllerBase
    {
        private readonly OrderCreationService _create;
        private readonly OrderPaymentService _pay;
        private readonly OrderCancellationService _cancel;
        private readonly OrderShippedService _ship;

        public OrdersController(
            OrderCreationService create,
            OrderPaymentService pay,
            OrderCancellationService cancel,
            OrderShippedService ship)
        {
            _create = create;
            _pay = pay;
            _cancel = cancel;
            _ship = ship;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<object>>> Create(CreateOrderRequest req)
        {
            var result = await _create.CreateAsync(req.UserId, req.Items);

            /*return Ok(ApiResponse<object>.Ok(new
            {
                orderId = result.OrderId,
                paymentExternalId = result.PaymentExternalId,
                message = "Order Created Successfully"
            }));*/
            return StatusCode(
                StatusCodes.Status201Created,
                ApiResponse<object>.Created(new
                {
                    orderId = result.OrderId,
                    paymentExternalId = result.PaymentExternalId
                }, "Order created successfully"));
        }


        [HttpPost("{id}/pay")]
        public async Task<ActionResult<ApiResponse<object>>> Pay( Guid id, [FromQuery] string paymentExternalId)
        {
            await _pay.PayAsync(id, paymentExternalId);

            return Ok(ApiResponse<object>.Ok(new
            {
                message = "Order paid Successfully"
            }));
        }

        [HttpPost("{id}/cancel")]
        public async Task<ActionResult<ApiResponse<object>>> Cancel(Guid id)
        {
            await _cancel.CancelAsync(id);

            return Ok(ApiResponse<object>.Ok(new
            {
                message = "Order Cancelled"
            }));
        }

        [HttpPost("{id}/ship")]
        public async Task<ActionResult<ApiResponse<object>>> Ship(Guid id)
        {
            await _ship.ShippedAsync(id);

            return Ok(ApiResponse<object>.Ok(new
            {
                message = "Order marked as shipped"
            }));
        }
    }
}
