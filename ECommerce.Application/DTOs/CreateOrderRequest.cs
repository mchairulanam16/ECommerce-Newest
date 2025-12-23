using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Application.DTOs
{
    public record CreateOrderRequest(
        [Required(ErrorMessage = "UserId is required")]
        Guid UserId,

        [Required(ErrorMessage = "Items is required")]
        [MinLength(1, ErrorMessage = "At least one item is required")]
        List<CreateOrderItem> Items
    );

    public record CreateOrderItem(
        [Required(ErrorMessage = "SKU is required")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "SKU must be between 1-50 characters")]
        string Sku,

        [Required(ErrorMessage = "Quantity is required")]
        [Range(1, 1000, ErrorMessage = "Quantity must be between 1-1000")]
        int Qty
    );

}
