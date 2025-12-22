using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Application.DTOs
{
    public record CreateOrderRequest(
    Guid UserId,
    List<CreateOrderItem> Items
    );

    public record CreateOrderItem(
        string Sku,
        int Qty
    );

}
