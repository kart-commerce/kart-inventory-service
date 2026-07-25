using KartInventoryService.Api.Grpc;
using KartInventoryService.Api.Middleware;
using KartInventoryService.Api.Observability;
using KartInventoryService.Api.Security;
using KartInventoryService.Application;
using KartInventoryService.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddGrpc();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddInventoryAuthentication();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Per-HTTP-request Information log (method/path/status/elapsed) - registered outermost, wrapping
// UseExceptionHandler below, so this always logs the *final* status code a client actually
// received.
app.UseSerilogRequestLogging();

// The single global error handler - every unhandled exception is translated to
// contracts/api-contract.yaml's Problem shape and logged here, so no controller/handler needs
// its own try/catch.
app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseMiddleware<InventoryContextEnrichmentMiddleware>();
app.UseAuthorization();

// Prometheus scrape target (observability-standards.md's mandatory `/metrics`).
app.MapPrometheusScrapingEndpoint();

app.MapControllers();

// INV-7: internal gRPC surface, consumed only by kart-cart-service.
app.MapGrpcService<InventoryAvailabilityGrpcService>();

app.Run();

// Exposed for WebApplicationFactory<Program> in IntegrationTests/ContractTests.
public partial class Program
{
}
