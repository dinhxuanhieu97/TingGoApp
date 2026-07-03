using Microsoft.EntityFrameworkCore;

namespace TingGo.SharedKernel.Persistence;

/// <summary>
/// Mỗi module implement để đăng ký entity/mapping của mình vào TingGoDbContext.
/// Đăng ký DI trong AddModule; Infrastructure sẽ gọi khi build model.
/// </summary>
public interface IModuleEntityConfigurator
{
    void Configure(ModelBuilder modelBuilder);
}
