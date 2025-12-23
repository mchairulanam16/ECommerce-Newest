using Microsoft.EntityFrameworkCore;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Application.Services;
using ECommerce.Api.Middlewares;
using ECommerce.Domain.Repositories;
using ECommerce.Infrastructure.Repositories;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using ECommerce.Api.Filters;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<ECommerceDbContext>(opt =>
{
    var conn = builder.Configuration.GetConnectionString("Default");
    //opt.UseMySql(conn, ServerVersion.AutoDetect(conn));
    /*opt.UseMySql(
        conn,
        ServerVersion.AutoDetect(conn),
        mySqlOptions =>
        {
            // Command timeout
            mySqlOptions.CommandTimeout(30);

            // Enable retry on failure (untuk network hiccups)
            mySqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null
            );

            // Migration assembly
            mySqlOptions.MigrationsAssembly("ECommerce.Infrastructure");
        }
    );*/
    opt.UseMySql(
       conn,
       ServerVersion.AutoDetect(conn),
       mySqlOptions =>
       {
           // Command timeout
           mySqlOptions.CommandTimeout(30);

           // Retry on transient failures
           mySqlOptions.EnableRetryOnFailure(
               maxRetryCount: 3,
               maxRetryDelay: TimeSpan.FromSeconds(5),
               errorNumbersToAdd: null
           );
       }
   );

    // Logging queries saat development (matikan di production)
    if (builder.Environment.IsDevelopment())
    {
        opt.EnableSensitiveDataLogging();
        opt.EnableDetailedErrors();
    }
});

builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddScoped<OrderCreationService>();
builder.Services.AddScoped<OrderPaymentService>();
builder.Services.AddScoped<OrderShippedService>();
builder.Services.AddScoped<OrderCancellationService>();
builder.Services.AddScoped<InventoryService>();

// Add controllers with validation formatting filter
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
});

// Configure API behavior to produce validation errors
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problemDetails = new ValidationProblemDetails(context.ModelState)
        {
            Title = "Validation failed",
            Status = StatusCodes.Status400BadRequest,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
        };

        return new BadRequestObjectResult(problemDetails);
    };
});

builder.Services.AddScoped<ValidationFilter>();


var app = builder.Build();
app.UseMiddleware<ApiExceptionMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
