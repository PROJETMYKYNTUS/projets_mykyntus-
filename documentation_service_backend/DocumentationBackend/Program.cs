using System.Data;
using System.Text.Json.Serialization;
using DocumentationBackend.Application.Abstractions;
using DocumentationBackend.Application.DocumentTemplates;
using DocumentationBackend.Configuration;
using DocumentationBackend.Context;
using DocumentationBackend.Data;
using DocumentationBackend.Infrastructure.Ai;
using DocumentationBackend.Infrastructure;
using DocumentationBackend.Infrastructure.Storage;
using DocumentationBackend.Messaging;
using DocumentationBackend.Middleware;
using DocumentationBackend.Services;
using Kyntus.Identity.Jwt;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("devCors", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins);
        }
        else
        {
            policy.WithOrigins("http://localhost:8200");
        }

        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders(
                "X-Document-Required-Total",
                "X-Document-Missing-Count",
                "X-Document-Filled-Count",
                "X-Document-Filled-Percent",
                "X-Document-Missing-Variables",
                "X-Document-Invalid-Count");
    });
});

builder.Services.AddKyntusJwtAuthentication(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<DocumentationCorrelationContext>();
builder.Services.AddScoped<DocumentationUserContext>();
builder.Services.AddScoped<IDocumentationTenantAccessor, DocumentationTenantAccessor>();
builder.Services.AddScoped<DocumentationWorkflowService>();
builder.Services.AddScoped<AiDirectDocumentFillOrchestrator>();
builder.Services.Configure<RibValidationOptions>(
    builder.Configuration.GetSection(RibValidationOptions.SectionName));
builder.Services.AddSingleton<IRibValidationService, RibValidationService>();
builder.Services.AddSingleton<ITemplatePlaceholderNormalizationService, TemplatePlaceholderNormalizationService>();
builder.Services.AddSingleton<ITemplateEngineService, TemplateEngineService>();
builder.Services.AddSingleton<IOriginalDocxTemplateRenderService, OriginalDocxTemplateRenderService>();
builder.Services.AddSingleton<IPdfExportService, PdfExportService>();

builder.Services.Configure<DocumentTemplatesInfrastructureOptions>(
    builder.Configuration.GetSection(DocumentTemplatesInfrastructureOptions.SectionName));
builder.Services.Configure<AiTemplateOptions>(
    builder.Configuration.GetSection(AiTemplateOptions.SectionPath));
builder.Services.Configure<DocumentBrandingOptions>(
    builder.Configuration.GetSection(DocumentBrandingOptions.SectionName));
builder.Services.Configure<DocumentWorkflowOptions>(
    builder.Configuration.GetSection(DocumentWorkflowOptions.SectionName));
builder.Services.AddSingleton<ITemplateBlobStorage, S3CompatibleTemplateBlobStorage>();
builder.Services.AddScoped<IAiApiKeyResolver, AiApiKeyResolver>();
builder.Services.AddHttpClient<IAiTemplateContentGenerator, OpenAiCompatibleTemplateContentGenerator>();
builder.Services.AddScoped<DirectoryUserSyncService>();
builder.Services.AddScoped<IDocumentTemplateManagementService, DocumentTemplateManagementService>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<EmployeDirectorySyncConsumer>();
    x.AddConsumer<OrgStructureDirectorySyncConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        cfg.ReceiveEndpoint("documentation-employe-sync", e =>
        {
            e.ConfigureConsumer<EmployeDirectorySyncConsumer>(ctx);
        });

        cfg.ReceiveEndpoint("documentation-org-structure", e =>
        {
            e.ConfigureConsumer<OrgStructureDirectorySyncConsumer>(ctx);
        });

        cfg.ConfigureEndpoints(ctx);
    });
});

var documentationCs = builder.Configuration.GetConnectionString("Documentation")
    ?? throw new InvalidOperationException("ConnectionStrings:Documentation manquante (voir appsettings).");

// Mot de passe hors chaîne ADO : évite les ambiguïtés avec caractères spéciaux (ex. !) et priorise DocumentationDb:Password.
var csb = new NpgsqlConnectionStringBuilder(documentationCs);
var documentationPassword = builder.Configuration["DocumentationDb:Password"];
if (!string.IsNullOrEmpty(documentationPassword))
    csb.Password = documentationPassword;

// Enregistrement des enums PostgreSQL (types créés dans le schéma « documentation »).
// Noms qualifiés obligatoires : sans « documentation. », Npgsql peut ne pas résoudre le type OID et lever une erreur à la lecture (500).
if (string.IsNullOrWhiteSpace(csb.SearchPath))
    csb.SearchPath = "documentation, public";

const string DocEnum = "documentation";
// Enums PostgreSQL : MapEnum sur NpgsqlDataSource (recommandé). Ne pas ajouter NpgsqlConnection.GlobalTypeMapper :
// obsolète, et en conflit avec cette source dédiée + EF HasPostgresEnum / HasColumnType.
var documentationDataSourceBuilder = new NpgsqlDataSourceBuilder(csb.ConnectionString);
documentationDataSourceBuilder.MapEnum<DocumentRequestStatus>($"{DocEnum}.document_request_status");
documentationDataSourceBuilder.MapEnum<GeneratedDocumentStatus>($"{DocEnum}.generated_document_status");
documentationDataSourceBuilder.MapEnum<WorkflowNotificationKey>($"{DocEnum}.workflow_notification_key");
documentationDataSourceBuilder.MapEnum<WorkflowActionKey>($"{DocEnum}.workflow_action_key");
documentationDataSourceBuilder.MapEnum<AppRole>($"{DocEnum}.app_role");
documentationDataSourceBuilder.MapEnum<StorageType>($"{DocEnum}.storage_type");
documentationDataSourceBuilder.MapEnum<DocumentTemplateKind>($"{DocEnum}.document_template_kind");
var documentationDataSource = documentationDataSourceBuilder.Build();
builder.Services.AddSingleton(documentationDataSource);

builder.Services.AddDbContext<DocumentationDbContext>((sp, options) =>
{
    options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(), npgsql =>
    {
        npgsql.MigrationsHistoryTable("__ef_migrations_history", "documentation");
        // EF Core 9+ : MapEnum sur le builder EF (en plus de NpgsqlDataSourceBuilder.MapEnum).
        // Sans cela, EF peut encore matérialiser les colonnes enum PostgreSQL comme Int32 → InvalidCastException.
        const string docSchema = "documentation";
        npgsql.MapEnum<AppRole>("app_role", docSchema);
        npgsql.MapEnum<DocumentRequestStatus>("document_request_status", docSchema);
        npgsql.MapEnum<GeneratedDocumentStatus>("generated_document_status", docSchema);
        npgsql.MapEnum<WorkflowNotificationKey>("workflow_notification_key", docSchema);
        npgsql.MapEnum<WorkflowActionKey>("workflow_action_key", docSchema);
        npgsql.MapEnum<StorageType>("storage_type", docSchema);
        npgsql.MapEnum<DocumentTemplateKind>("document_template_kind", docSchema);
    });
    options.UseSnakeCaseNamingConvention();
});

var app = builder.Build();

// Docker / première installation : init.sql ne crée que la base utilisateur — pas les tables métier.
// Sans schéma, les endpoints /api/documentation/data/* renvoient 42P01 et le front affiche « Impossible de charger les demandes ».
if (app.Configuration.GetValue("Documentation:ApplyBootstrapSchema", false))
{
    await using var bootstrapScope = app.Services.CreateAsyncScope();
    var bootstrapLog = bootstrapScope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Documentation.SchemaBootstrap");
    var db = bootstrapScope.ServiceProvider.GetRequiredService<DocumentationDbContext>();
    try
    {
        var created = await db.Database.EnsureCreatedAsync();
        bootstrapLog.LogInformation("Documentation:ApplyBootstrapSchema=true — EnsureCreated returned {Created}", created);
    }
    catch (Exception ex)
    {
        bootstrapLog.LogCritical(ex, "Documentation:ApplyBootstrapSchema — EnsureCreated failed");
        throw;
    }

    try
    {
        await DocumentationSchemaBootstrap.ApplyPostSchemaObjectsAsync(db, bootstrapLog);
    }
    catch (Exception ex)
    {
        bootstrapLog.LogWarning(ex, "Documentation:ApplyBootstrapSchema — fonction REQ non appliquée (non bloquant).");
    }
}

// Fonctions SQL (numérotation REQ) : idempotent même si EnsureCreated déjà exécuté auparavant.
if (app.Configuration.GetValue("Documentation:ApplyPostSchemaObjects", true))
{
    await using var postSchemaScope = app.Services.CreateAsyncScope();
    var postSchemaLog = postSchemaScope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Documentation.SchemaBootstrap");
    var postSchemaDb = postSchemaScope.ServiceProvider.GetRequiredService<DocumentationDbContext>();
    try
    {
        await DocumentationSchemaBootstrap.ApplyPostSchemaObjectsAsync(postSchemaDb, postSchemaLog);
    }
    catch (Exception ex)
    {
        postSchemaLog.LogWarning(ex, "Documentation:ApplyPostSchemaObjects — fonction REQ non appliquée (schéma absent ?).");
    }
}

if (app.Configuration.GetValue("Documentation:DemoDataSeed", false))
{
    await using var demoSeedScope = app.Services.CreateAsyncScope();
    var demoDb = demoSeedScope.ServiceProvider.GetRequiredService<DocumentationDbContext>();
    var demoLog = demoSeedScope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Documentation.DemoDataSeed");
    await DockerDocumentationDemoDataSeed.ApplyIfEnabledAsync(app.Configuration, demoDb, demoLog);
}

app.UseMiddleware<UnhandledExceptionMiddleware>();
app.UseCors("devCors");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<DocumentationCorrelationMiddleware>();
app.UseMiddleware<DocumentationUserContextMiddleware>();

app.MapGet("/health", () => Results.Json(new { status = "Healthy", service = "documentation" }));
app.MapGet("/healthz", () => Results.Json(new { status = "Healthy", service = "documentation" }));
app.MapGet("/ready", () => Results.Json(new { status = "Ready", service = "documentation" }));
app.MapGet("/api/documentation/health", () => Results.Json(new { status = "Healthy", service = "documentation" }));

// GET / n’avait aucune route → 404 dans les logs quand on ouvre http://localhost:5002/ dans le navigateur.
app.MapGet("/", () => Results.Json(new
{
    service = "DocumentationBackend",
    message = "API opérationnelle. Il n’y a pas de page HTML ici.",
    tryThese = new[] { "/health", "/api/documentation/db/status", "/api/documentation/health" },
}));

app.MapControllers();

app.MapGet("/api/documentation/db/status", async (
    DocumentationDbContext db,
    IConfiguration configuration,
    IHostEnvironment hostEnvironment,
    CancellationToken ct) =>
{
    var cs = configuration.GetConnectionString("Documentation") ?? "";
    var csb = new NpgsqlConnectionStringBuilder(cs);
    var statusPwd = configuration["DocumentationDb:Password"];
    if (!string.IsNullOrEmpty(statusPwd))
        csb.Password = statusPwd;

    try
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);
        try
        {
            var documentTypeCount = await db.DocumentTypes.CountAsync(ct);
            var documentRequestCount = await db.DocumentRequests.CountAsync(ct);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT current_database()";
            var serverDbName = (await cmd.ExecuteScalarAsync(ct))?.ToString() ?? "";
            cmd.CommandText = "SELECT count(*)::bigint FROM documentation.document_requests";
            var documentRequestTotalAllTenants = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct) ?? 0L);
            return Results.Ok(new
            {
                connected = true,
                schema = "documentation",
                serverDatabase = serverDbName,
                configuredHost = csb.Host,
                configuredPort = csb.Port,
                configuredDatabase = csb.Database,
                documentTypeCount,
                documentRequestCount,
                documentRequestTotalAllTenants,
                hint =
                    "Comparer serverDatabase / host / port avec la connexion pgAdmin. " +
                    "documentRequestCount suit le filtre tenant courant ; documentRequestTotalAllTenants compte toutes les lignes.",
            });
        }
        finally
        {
            if (connection.State == ConnectionState.Open)
                await connection.CloseAsync();
        }
    }
    catch (Exception ex)
    {
        if (!hostEnvironment.IsDevelopment())
        {
            return Results.Ok(new
            {
                connected = false,
                schema = "documentation",
                message = "Impossible de joindre PostgreSQL.",
            });
        }

        return Results.Ok(new
        {
            connected = false,
            schema = "documentation",
            message = "Impossible de joindre PostgreSQL.",
            errorType = ex.GetType().Name,
            errorMessage = ex.Message,
            host = csb.Host,
            port = csb.Port,
            database = csb.Database,
            username = csb.Username,
            passwordConfigured = !string.IsNullOrEmpty(csb.Password),
            passwordFromDocumentationDbKey = !string.IsNullOrEmpty(configuration["DocumentationDb:Password"]),
            hint = "28P01 = mot de passe refusé par PostgreSQL. Si le mot de passe contient des caractères spéciaux, placez-le dans DocumentationDb:Password (JSON). Sinon alignez le mot de passe : ALTER USER postgres WITH PASSWORD 'votre_mot_de_passe'; (en superutilisateur).",
        });
    }
});

app.Run();
