using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Prime.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPrimeApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        return services;
    }
}
