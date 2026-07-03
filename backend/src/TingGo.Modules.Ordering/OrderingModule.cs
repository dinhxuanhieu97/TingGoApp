using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TingGo.Modules.Ordering.Endpoints;
using TingGo.Modules.Ordering.Persistence;
using TingGo.Modules.Ordering.Services;
using TingGo.SharedKernel.Modules;
using TingGo.SharedKernel.Persistence;

namespace TingGo.Modules.Ordering;

public sealed class OrderingModule : IModule
{
    public string Name => "Ordering";

    public IServiceCollection AddModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModuleEntityConfigurator, OrderingEntityConfigurator>();
        services.AddSingleton<SessionTokenService>();
        services.AddScoped<OrderService>();
        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        PublicOrderEndpoints.Map(endpoints);
        MerchantOrderEndpoints.Map(endpoints);
        ServiceRequestEndpoints.Map(endpoints);
        SessionEndpoints.Map(endpoints);
        return endpoints;
    }
}
