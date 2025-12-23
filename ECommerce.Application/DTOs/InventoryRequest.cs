using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Application.DTOs
{
    public record InventoryRequest(
        [Required(ErrorMessage = "SKU is required")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "SKU must be between 1-50 characters")]
        string Sku,

        [Required(ErrorMessage = "ActualQty is required")]
        [Range(0, int.MaxValue, ErrorMessage = "ActualQty cannot be negative")]
        int ActualQty,

        [Required(ErrorMessage = "ReserveQty is required")]
        [Range(0, int.MaxValue, ErrorMessage = "ReserveQty cannot be negative")]
        int ReserveQty
    );
}
