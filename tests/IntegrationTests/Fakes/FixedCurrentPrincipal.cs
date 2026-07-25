using KartInventoryService.Application.Common.Interfaces;

namespace KartInventoryService.IntegrationTests.Fakes;

public sealed class FixedCurrentPrincipal : ICurrentPrincipal
{
    public FixedCurrentPrincipal(string actingPrincipal) => ActingPrincipal = actingPrincipal;

    public string ActingPrincipal { get; }
}
