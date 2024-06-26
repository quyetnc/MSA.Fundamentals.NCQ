using MSA.OrderService.Domain;
using MSA.OrderService.Infrastructure.Data;
using MSA.Common.Contracts.Settings;
using MSA.Common.PostgresMassTransit.PostgresDB;
using MSA.OrderService.Services;
using MSA.Common.PostgresMassTransit.MassTransit;
using MSA.OrderService.StateMachine;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using MSA.OrderService.Infrastructure.Saga;
using System.Reflection;
var builder = WebApplication.CreateBuilder(args);
PostgresDBSetting serviceSetting = builder.Configuration.GetSection(nameof(PostgresDBSetting)).Get<PostgresDBSetting>();

// Add services to the container.
builder.Services
  .AddPostgres<MainDbContext>()
    .AddPostgresRepositories<MainDbContext, Order>()
    .AddPostgresRepositories<MainDbContext, Product>()
    .AddPostgresUnitofWork<MainDbContext>()
    //  .AddMassTransitWithRabbitMQ();
    .AddMassTransitWithPostgresOutbox<MainDbContext>(cfg =>
    {
        cfg.AddSagaStateMachine<OrderStateMachine, OrderState>()
              .EntityFrameworkRepository(r =>
              {
                  r.ConcurrencyMode = ConcurrencyMode.Pessimistic;

                  r.LockStatementProvider = new PostgresLockStatementProvider();

                  r.AddDbContext<DbContext, OrderStateDbContext>((provider, builder) =>
                   {
                       builder.UseNpgsql(serviceSetting.ConnectionString, n =>
                       {
                           n.MigrationsAssembly(Assembly.GetExecutingAssembly().GetName().Name);
                           n.MigrationsHistoryTable($"__{nameof(OrderStateDbContext)}");
                       });
                   });
              });
    });

// Configure HttpClientHandler with custom SSL settings
var handler = new HttpClientHandler
{
    // Ignore SSL certificate validation errors
    ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
};

// Add the typed HttpClient with configured base address and custom HttpClientHandler
builder.Services.AddHttpClient<IProductService, ProductService>(client =>
{
    client.BaseAddress = new Uri("https://localhost:5002");
}).ConfigurePrimaryHttpMessageHandler(() => handler);

builder.Services.AddControllers(opt =>
{
    opt.SuppressAsyncSuffixInActionNames = false;
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();
app.MapControllers();
app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
