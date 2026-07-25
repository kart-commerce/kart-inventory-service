using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Formatting.Compact;

namespace KartInventoryService.Api.Observability;

/// <summary>
/// observability-standards.md's mandated stack (Serilog -> Loki, OpenTelemetry -> Tempo/
/// Prometheus), wired locally in this composition root - kart-conventions.md's
/// `Kart.Shared.Observability` doesn't exist as a published, adopted package yet (no sibling
/// service consumes it), so this mirrors kart-identity-service/kart-category-service's own
/// interim wiring, ready to be swapped for the shared package later.
///
/// Unlike kart-category-service, this service explicitly forces 100%-trace sampling
/// (<see cref="AlwaysOnSampler"/>): kart-conventions.md's Observability section names
/// kart-inventory-service as one of the four Order-Saga participants required to sample at
/// 100% (the same tier as kart-order-service/kart-payment-service/kart-shipping-service), and
/// Kart.Shared.Observability's own README documents that it has no sampling-tier knob yet - each
/// of those four services must configure this locally until it does.
/// </summary>
public static class ObservabilityExtensions
{
    public const string ServiceName = "kart-inventory-service";

    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        var otlpEndpoint = builder.Configuration["Observability:Otlp:Endpoint"];

        // Console sink emits structured JSON; shipping to Loki is the OTel Collector's job.
        builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithSpan()
            .Enrich.WithProperty("service", ServiceName)
            .WriteTo.Console(new CompactJsonFormatter()));

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing =>
            {
                tracing
                    // 100%-trace-coverage tier (kart-conventions.md) - Inventory's reserve/release
                    // calls are a synchronous step directly on the Order Saga's critical path.
                    .SetSampler(new AlwaysOnSampler())
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
                }
            })
            .WithMetrics(metrics =>
            {
                // RED metrics (rate/errors/duration) on every HTTP endpoint, per
                // observability-standards.md - ASP.NET Core's own instrumentation already emits
                // http.server.request.duration; scraped at /metrics.
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddPrometheusExporter();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    metrics.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
                }
            });

        return builder;
    }
}
