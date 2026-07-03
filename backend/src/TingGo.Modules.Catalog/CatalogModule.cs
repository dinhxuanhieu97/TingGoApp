using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TingGo.Modules.Catalog.Endpoints;
using TingGo.Modules.Catalog.Persistence;
using TingGo.Modules.Catalog.Services;
using TingGo.SharedKernel.Modules;
using TingGo.SharedKernel.Persistence;

namespace TingGo.Modules.Catalog;

public sealed class CatalogModule : IModule
{
    public string Name => "Catalog";

    public IServiceCollection AddModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModuleEntityConfigurator, CatalogEntityConfigurator>();
        services.AddScoped<CatalogGuard>();
        services.AddSingleton<IImageStorage, LocalImageStorage>();
        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        MenuEndpoints.Map(endpoints);
        ProductEndpoints.Map(endpoints);
        ModifierEndpoints.Map(endpoints);
        FileEndpoints.Map(endpoints);
        PublicMenuEndpoints.Map(endpoints);
        return endpoints;
    }
}
