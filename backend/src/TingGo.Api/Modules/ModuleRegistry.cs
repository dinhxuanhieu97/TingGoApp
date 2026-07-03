using TingGo.Modules.Catalog;
using TingGo.Modules.Identity;
using TingGo.Modules.Notifications;
using TingGo.Modules.Ordering;
using TingGo.Modules.Payments;
using TingGo.Modules.Venues;
using TingGo.SharedKernel.Modules;

namespace TingGo.Api.Modules;

public static class ModuleRegistry
{
    public static IReadOnlyList<IModule> Modules { get; } =
    [
        new IdentityModule(),
        new VenuesModule(),
        new CatalogModule(),
        new OrderingModule(),
        new PaymentsModule(),
        new NotificationsModule(),
    ];
}
