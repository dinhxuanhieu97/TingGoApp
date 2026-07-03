using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TingGo.SharedKernel.Modules;

namespace TingGo.Modules.Payments;

public sealed class PaymentsModule : IModule
{
    public string Name => "Payments";

    public IServiceCollection AddModule(IServiceCollection services, IConfiguration configuration)
    {
        // Đăng ký service của module (bổ sung theo sprint).
        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Map endpoint của module dưới /api/v1 (bổ sung theo sprint).
        return endpoints;
    }
}
