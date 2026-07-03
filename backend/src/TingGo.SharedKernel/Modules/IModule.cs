using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TingGo.SharedKernel.Modules;

/// <summary>
/// Hợp đồng cho mỗi module trong modular monolith.
/// Module chỉ được tham chiếu SharedKernel; giao tiếp giữa module qua events/contracts.
/// </summary>
public interface IModule
{
    string Name { get; }

    IServiceCollection AddModule(IServiceCollection services, IConfiguration configuration);

    IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints);
}
