using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Parrainage.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddParrainageApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        return services;
    }
}
