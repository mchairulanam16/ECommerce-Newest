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
  
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ECommerceDbContext _db;

        public UnitOfWork(ECommerceDbContext db)
        {
            _db = db;
        }

        /*public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action)
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var result = await action();
                await tx.CommitAsync();
                return result;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }*/

        public async Task ExecuteInTransactionAsync(Func<Task> action)
        {
            await ExecuteInTransactionAsync(async () =>
            {
                await action();
                return Task.CompletedTask;
            });
        }
        public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation)
        {
            var executionStrategy = _db.Database.CreateExecutionStrategy();

            return await executionStrategy.ExecuteAsync(async () =>
            {
                using var transaction = await _db.Database.BeginTransactionAsync();

                try
                {
                    var result = await operation();
                    await _db.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return result;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task SaveChangesAsync()
        {
            await _db.SaveChangesAsync();
        }
    }
}
