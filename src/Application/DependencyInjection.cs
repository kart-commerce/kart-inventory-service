using FluentValidation;
using KartInventoryService.Application.Common.Behaviors;
using KartInventoryService.Application.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace KartInventoryService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);

            // Registration order is pipeline order (outermost first) - Logging wraps Validation
            // so every request's completion/duration is observed uniformly.
            configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddSingleton(TimeProvider.System);

        // Shared release-path service backing INV-2/INV-3/INV-4/INV-5 (tickets.md's Sprint
        // Planner note) - depends only on Application abstractions + Domain, no Infrastructure
        // concrete types, so it belongs here rather than in a MediatR handler.
        services.AddScoped<ReservationReleaseService>();

        // Inventory & Stock Management flow's "Deduct (Order Confirmed)" counterpart to the
        // release service above.
        services.AddScoped<ReservationCommitService>();

        return services;
    }
}
