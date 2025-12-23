using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ECommerce.Infrastructure.Persistence;
using System.Linq;

namespace ECommerce.Tests.Integration
{
    public class CustomWebApplicationFactory : WebApplicationFactory<ECommerce.Api.DummyProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            /*builder.ConfigureServices(services =>
            {
                // Remove the existing DbContext registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ECommerceDbContext>));

                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add in-memory database for testing
                services.AddDbContext<ECommerceDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
                });

                // Build the service provider
                var sp = services.BuildServiceProvider();

                // Create a scope to obtain a reference to the database context
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

                // Ensure the database is created
                db.Database.EnsureCreated();

                // Bersihkan data sebelumnya
                db.Database.EnsureDeleted();
            });*/
            builder.ConfigureServices(services =>
            {
                // Remove existing DbContext configuration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ECommerceDbContext>));

                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add InMemory database
                services.AddDbContext<ECommerceDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDatabase_" + Guid.NewGuid()); // Gunakan unique name
                });

                // JANGAN panggil BuildServiceProvider() di sini!
                // JANGAN panggil EnsureCreated() di sini!
                // Biarkan framework yang handle
            });
        }
    }
}