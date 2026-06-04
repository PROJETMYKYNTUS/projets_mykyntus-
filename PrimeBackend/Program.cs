using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using Kyntus.Identity.Jwt;
using PrimeBackend.Infrastructure;
using PrimeBackend.Services;

var isEnrichCli = args.Length > 0 && args[0] == "enrich-demo";
var builder = isEnrichCli
    ? WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        EnvironmentName = Environments.Development,
    })
    : WebApplication.CreateBuilder(args);

if (isEnrichCli)
{
    await RunEnrichDemoCliAsync(builder, args);
    return;
}

static async Task RunEnrichDemoCliAsync(WebApplicationBuilder builder, string[] args)
{
    var conn = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(conn))
    {
        conn = "Host=localhost;Port=8433;Database=prime_db;Username=prime_user;Password=Prime@2026";
        Console.WriteLine("ConnectionStrings:DefaultConnection absente — repli localhost:8433.");
    }

    builder.Services.AddLogging(b => b.AddConsole());
    builder.Services.AddDbContext<PrimeDbContext>(o => o.UseNpgsql(conn));
    builder.Services.AddScoped<PrimeOrgScopeService>(sp => new PrimeOrgScopeService(sp.GetService<PrimeDbContext>()));
    builder.Services.AddScoped<PrimeValidationWorkflowRuntime>();
    builder.Services.AddScoped<PrimeFicheValidationSubmissionService>();
    await using var app = builder.Build();
    var force = args.Contains("--force", StringComparer.OrdinalIgnoreCase);
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
    var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("enrich-demo");
    await db.Database.MigrateAsync();
    await PrimeSchemaPatches.EnsureOrgOptionalAndDraftRootPoleAsync(db);
    var result = await PrimeDbEnrichmentSeeder.EnrichAsync(db, force, CancellationToken.None, log);
    var submission = scope.ServiceProvider.GetService<PrimeFicheValidationSubmissionService>();
    if (submission is not null)
        await PrimeValidationDemoRepair.ApplyAsync(db, submission, log, CancellationToken.None);
    var counts = await PrimeDbEnrichmentSeeder.SnapshotCountsAsync(db);
    Console.WriteLine(result.Applied
        ? $"Enrichissement PRIME v{PrimeDbEnrichmentSeeder.Version} appliqué. Fiches={counts.Fiches}, audit={counts.AuditLogs}, anomalies={counts.Anomalies}, pilotes enrich={counts.EnrichEmployees}"
        : $"Enrichissement ignoré ({result.Reason}). Fiches={counts.Fiches}. Utilisez --force pour réappliquer.");
}

builder.Services.AddControllers(options =>
{
    var policy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter(policy));
}).AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("devCors", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:8200",
                "http://localhost:8201",
                "http://localhost:8202",
                "http://localhost:8203",
                "http://localhost:4207")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddKyntusJwtAuthentication(builder.Configuration);

builder.Services.AddSingleton<PrimeInMemoryStore>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<PrimeAuditLogService>();
builder.Services.AddScoped<PrimeRpQueryService>(sp =>
    new PrimeRpQueryService(sp.GetService<PrimeDbContext>()));
builder.Services.AddScoped<PrimeOrgScopeService>(sp =>
    new PrimeOrgScopeService(sp.GetService<PrimeDbContext>()));

var conn = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(conn))
{
    builder.Services.AddDbContext<PrimeDbContext>(o => o.UseNpgsql(conn));
    if (builder.Configuration.GetValue("Prime:ApplyMigrations", true))
        builder.Services.AddHostedService<PrimeDatabaseInitializer>();

    builder.Services.AddScoped<PrimeBackend.Services.AnomalyDetectionService>();
    builder.Services.AddScoped<PrimeValidationWorkflowRuntime>();
    builder.Services.AddScoped<PrimeFicheValidationSubmissionService>();
    builder.Services.AddScoped<PrimeValidationListService>();
    builder.Services.AddScoped<PrimeFicheValidationHistoryService>();
    builder.Services.AddScoped<PrimeRbacReadService>();
    builder.Services.AddScoped<IPrimeRequestUserResolver, PrimeRequestUserResolver>();
    builder.Services.AddScoped<GlobalPoolWorkflowService>();
    builder.Services.AddScoped<PrimeGlobalSynthesisReadinessService>();
    builder.Services.AddScoped<PrimeGlobalSynthesisService>();
    builder.Services.AddScoped<PrimeGlobalSynthesisLineService>();
    builder.Services.AddScoped<PrimeGlobalSynthesisPaymentService>();
    builder.Services.AddScoped<PrimeFicheMergedPreviewAccessService>();
}

var app = builder.Build();
app.UseCors("devCors");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ApiExceptionMiddleware>();
app.MapControllers();
app.Run();
