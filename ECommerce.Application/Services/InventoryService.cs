using ECommerce.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ECommerce.Application.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services
{
    public class InventoryService
    {
        private readonly ECommerceDbContext _db;

        public InventoryService(ECommerceDbContext db)
        {
            _db = db;
        }

        public async Task<InventoryRequest?> GetBySkuAsync(string sku)
        {
            return await _db.Inventory
                .AsNoTracking()
                .Where(x => x.Sku == sku)
                .Select(x => new InventoryRequest(
                    x.Sku,
                    x.ActualQty,
                    x.ReservedQty
                ))
                .FirstOrDefaultAsync();
        }
    }
}
