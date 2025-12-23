using ECommerce.Domain.Entities;
using ECommerce.Domain.Exceptions;
using ECommerce.Domain.Repositories;
using ECommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Infrastructure.Repositories
{
    public class InventoryRepository : IInventoryRepository
    {
        private readonly ECommerceDbContext _db;

        public InventoryRepository(ECommerceDbContext db)
        {
            _db = db;
        }

        public async Task<InventoryItem> GetBySkuAsync(string sku)
        {
            return await _db.Inventory
                .FirstOrDefaultAsync(x => x.Sku == sku) 
                ?? throw new KeyNotFoundException($"Inventory {sku} not found");
        }

        public async Task<bool> TryReserveAsyncRaw(string sku, int qty)
        {
            var affected = await _db.Database.ExecuteSqlRawAsync(
                @"UPDATE Inventory
            SET ReservedQty = ReservedQty + {0}
            WHERE Sku = {1}
            AND ActualQty - ReservedQty >= {0}",
                qty, sku);

            return affected > 0;
        }
        public async Task<bool> TryReserveAsync(string sku, int qty)
        {
            var inv = await _db.Inventory
                .SingleAsync(x => x.Sku == sku);

            inv.Reserve(qty);

            try
            {
                await _db.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                return false;
            }
        }

        public async Task<bool> TryReserveWithRetryAsyncOld(string sku, int qty)
        {
            const int MaxRetryAttempts = 3;
            for (int attempt = 0; attempt < MaxRetryAttempts; attempt++)
            {
                try
                {
                    var inv = await _db.Inventory
                        .FirstOrDefaultAsync(x => x.Sku == sku);

                    if (inv == null)
                        throw new KeyNotFoundException($"Inventory {sku} not found");

                    inv.Reserve(qty); // Will throw if insufficient stock

                    await _db.SaveChangesAsync();
                    return true;
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (attempt == MaxRetryAttempts - 1)
                        return false;

                    _db.ChangeTracker.Clear();

                    // Small delay before retry
                    await Task.Delay(10 * (attempt + 1));
                }
                /*catch (DomainException) // Out of stock
                {
                    return false;
                }*/
            }

            return false;
        }
        public async Task<bool> TryReserveWithRetryAsync(string sku, int qty)
        {
            var executionStrategy = _db.Database.CreateExecutionStrategy();

            return await executionStrategy.ExecuteAsync(async () =>
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        //using var transaction = await _db.Database.BeginTransactionAsync();

                        var inv = await _db.Inventory
                            .FirstOrDefaultAsync(x => x.Sku == sku);

                        if (inv == null)
                            throw new KeyNotFoundException($"Inventory {sku} not found");

                        inv.Reserve(qty); // Will throw if insufficient stock

                        await _db.SaveChangesAsync();
                        //await transaction.CommitAsync();
                        return true;
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        if (attempt == 2)
                            return false;

                        _db.ChangeTracker.Clear();
                        await Task.Delay(10 * (attempt + 1));
                    }
                    catch (Exception)
                    {
                        // Handle other exceptions
                        return false;
                    }
                }

                return false;
            });
        }

        public async Task ReleaseAsync(string sku, int qty)
        {
            var inv = await GetBySkuAsync(sku);
            inv.Release(qty);
        }

        public async Task CommitAsync(string sku, int qty)
        {
            var inv = await GetBySkuAsync(sku);
            inv.Commit(qty);
        }

        public async Task ReleaseAsyncRaw(string sku, int qty)
        {
            var affected = await _db.Database.ExecuteSqlRawAsync(
                @"UPDATE Inventory
            SET ReservedQty = ReservedQty - {0}
            WHERE Sku = {1} AND ReservedQty >= {0}",
                qty, sku);

            if (affected == 0)
                throw new InvalidOperationException($"Cannot release {qty} from {sku}");
        }

        public async Task CommitAsyncRaw(string sku, int qty)
        {
            var affected = await _db.Database.ExecuteSqlRawAsync(
                @"UPDATE Inventory
            SET ActualQty = ActualQty - {0},
                ReservedQty = ReservedQty - {0}
            WHERE Sku = {1} AND ReservedQty >= {0}",
                qty, sku);

            if (affected == 0)
                throw new InvalidOperationException($"Cannot commit {qty} for {sku}");
        }
    }
}
