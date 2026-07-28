using KartInventoryService.Api.Grpc;
using KartInventoryService.Api.Middleware;
using KartInventoryService.Api.Security;
using KartInventoryService.Application;
using KartInventoryService.Infrastructure;
using Kart.Shared.Configuration;
using Kart.Shared.Observability;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// kart-conventions.md Configuration Management: GlobalConfig external-secrets-file bootstrap,
// shared across every service - never reimplemented per service. See appsettings.Local.json.example.
builder.AddKartGlobalConfig();

// kart-conventions.md Observability section: Serilog + OpenTelemetry SDK behind one DI call.
builder.AddKartObservability("kart-inventory-service");

// Kart.Shared.Observability doesn't yet expose a sampling-tier knob (see its README's "Known
// gap"/100%-trace-coverage note) - kart-inventory-service is one of the four Order-Saga
// participants (alongside kart-order-service/kart-payment-service/kart-shipping-service) required
// by kart-conventions.md to sample 100% of traces, since reserve/release is a synchronous step
// directly on the Order Saga's critical path. Re-apply the always-on sampler after
// AddKartObservability has already built the tracer provider, until the shared package grows this
// option itself.
builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.SetSampler(new AlwaysOnSampler()));

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
