using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ECommerce.Domain.Entities;

namespace ECommerce.Domain.Repositories
{
    public interface IInventoryRepository
    {
        Task<InventoryItem> GetBySkuAsync(string sku);
        Task<bool> TryReserveAsync(string sku, int qty);
        Task<bool> TryReserveAsyncRaw(string sku, int qty);
        Task<bool> TryReserveWithRetryAsync(string sku, int qty);
        Task ReleaseAsync(string sku, int qty);
        Task CommitAsync(string sku, int qty);
        Task ReleaseAsyncRaw(string sku, int qty);
        Task CommitAsyncRaw(string sku, int qty);
    }
}
