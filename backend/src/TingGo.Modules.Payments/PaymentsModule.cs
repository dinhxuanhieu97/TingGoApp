using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TingGo.Modules.Payments.Endpoints;
using TingGo.Modules.Payments.Persistence;
using TingGo.SharedKernel.Modules;
using TingGo.SharedKernel.Persistence;

namespace TingGo.Modules.Payments;

public sealed class PaymentsModule : IModule
{
    public string Name => "Payments";

    public IServiceCollection AddModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModuleEntityConfigurator, PaymentsEntityConfigurator>();
        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        PaymentEndpoints.Map(endpoints);
        return endpoints;
    }
}
