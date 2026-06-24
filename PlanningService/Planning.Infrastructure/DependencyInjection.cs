using Kyntus.Iam;
using Kyntus.Messaging.Outbox;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Planning.Application.Abstractions;
using Planning.Application.Abstractions.EmployeeImport;
using Planning.Infrastructure.Messaging.Consumers;
using Planning.Infrastructure.Messaging.Publishers;
using Planning.Infrastructure.Persistence;
using Planning.Infrastructure.Services;
using Planning.Infrastructure.Services.EmployeeImport;
using PlanningServiceImpl = Planning.Infrastructure.Services.PlanningService;

namespace Planning.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPlanningInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isTesting)
    {
        if (isTesting)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(configuration.GetConnectionString("DefaultConnection")
                    ?? "DataSource=planning_characterization_test.db"));
        }
        else
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
        }

        services.AddKyntusOutbox<AppDbContext>();
        services.AddKyntusIamViaDirectoryHttp(
            configuration["Directory:BaseUrl"] ?? "http://employee-directory-backend:8080");

        services.AddScoped<IFloorService, FloorService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPlanningCongeService, PlanningCongeService>();
        services.AddScoped<IUserLegacyExcelService, UserLegacyExcelService>();
        services.AddScoped<IServiceService, ServiceService>();
        services.AddScoped<ISubServiceService, SubServiceService>();
        services.AddHttpClient("DirectorySync");
        services.AddScoped<IDirectoryEmployeeEnsureClient, DirectoryEmployeeEnsureClient>();
        services.AddScoped<IDirectoryEmployeeWriteClient, DirectoryEmployeeWriteClient>();
        services.AddScoped<IDirectoryHierarchyClient, DirectoryHierarchyClient>();
        services.AddHttpClient<IUserService, UserService>(client =>
        {
            client.BaseAddress = new Uri("http://kyntus_auth_backend:8080/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<IContractService, ContractService>();
        services.AddScoped<IPlanningOrgMirrorService, PlanningOrgMirrorService>();
        services.AddScoped<IOrgReconciliationService, OrgReconciliationService>();
        services.AddScoped<IPlanningService, PlanningServiceImpl>();
        services.AddScoped<IPlanningOrgMirrorService, PlanningOrgMirrorService>();
        services.AddScoped<IDirectoryOrgWriteClient, DirectoryOrgWriteClient>();

        services.AddScoped<IEmployeeFieldService, EmployeeFieldService>();
        services.AddScoped<IEmployeeImportConfigService, EmployeeImportConfigService>();
        services.AddScoped<IEmployeeImportSessionStore, EmployeeImportSessionStore>();
        services.AddScoped<IEmployeeImportOrgResolver, EmployeeImportOrgResolver>();
        services.AddScoped<IEmployeeImportOrgGapAnalyzer, EmployeeImportOrgGapAnalyzer>();
        services.AddScoped<IEmployeeImportOrgProvisioner, EmployeeImportDirectoryOrgProvisioner>();
        services.AddScoped<IEmployeeImportStructureAssignmentService, EmployeeImportStructureAssignmentService>();
        services.AddScoped<IEmployeeImportExecutor, EmployeeImportExecutor>();
        services.AddScoped<IEmployeeImportService, EmployeeImportService>();
        services.AddScoped<IEmployeeImportUserPersistence, EmployeeImportUserPersistence>();
        services.AddSingleton<EmployeeImportFileParser>();
        services.AddSingleton<EmployeeImportColumnMatcher>();
        services.AddScoped<EmployeeImportTemplateBuilder>();

        services.AddScoped<IReclamationService, ReclamationService>();
        services.AddScoped<IPropositionService, PropositionService>();
        services.AddScoped<IReclamationNotificationService, ReclamationNotificationService>();
        services.AddScoped<INewsletterService, NewsletterService>();
        services.AddScoped<IEmployePublisher, EmployePublisher>();

        if (isTesting)
        {
            services.AddMassTransit(x => x.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx)));
        }
        else
        {
            services.AddMassTransit(x =>
            {
                x.AddConsumer<CongeValideConsumer>();
                x.AddConsumer<OrgStructureConsumer>();
                x.AddConsumer<DirectoryEmployeeProjectionConsumer>();
                x.AddConsumer<PlanningDirectoryOrgProjectionConsumer>();

                x.UsingRabbitMq((ctx, cfg) =>
                {
                    cfg.Host(configuration["RabbitMQ:Host"] ?? "rabbitmq", "/", h =>
                    {
                        h.Username(configuration["RabbitMQ:Username"] ?? "guest");
                        h.Password(configuration["RabbitMQ:Password"] ?? "guest");
                    });

                    cfg.ReceiveEndpoint("planning-conge-valide", e =>
                        e.ConfigureConsumer<CongeValideConsumer>(ctx));
                    cfg.ReceiveEndpoint("planning-org-structure", e =>
                        e.ConfigureConsumer<OrgStructureConsumer>(ctx));
                    cfg.ReceiveEndpoint("planning-directory-employee", e =>
                        e.ConfigureConsumer<DirectoryEmployeeProjectionConsumer>(ctx));
                    cfg.ReceiveEndpoint("planning-directory-org", e =>
                        e.ConfigureConsumer<PlanningDirectoryOrgProjectionConsumer>(ctx));

                    cfg.ConfigureEndpoints(ctx);
                });
            });
        }

        return services;
    }
}
