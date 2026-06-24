using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Documentation.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddDocumentationApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        return services;
    }
}
