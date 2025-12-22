using ECommerce.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ECommerce.Application.DTOs;
using System.ComponentModel.DataAnnotations;
using ECommerce.Application.Services;
using ECommerce.Api.Contracts;

namespace ECommerce.Api.Controllers
{
    [ApiController]
    [Route("inventory")]
    public class InventoryController : ControllerBase
    {
        private readonly InventoryService _service;

        public InventoryController(
            InventoryService create)
        {
            _service = create;
        }

        [HttpGet("{sku}")]
        public async Task<ActionResult<ApiResponse<InventoryRequest>>> Get( [FromRoute, Required, StringLength(50, MinimumLength = 1)] string sku)
        {
            var item = await _service.GetBySkuAsync(sku);

            if (item == null)
                throw new KeyNotFoundException($"Inventory {sku} not found");

            return Ok(ApiResponse<InventoryRequest>.Ok(item));
        }

    }

}
