using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
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
        conn = "Host=localhost;Port=5433;Database=prime_db;Username=prime_user;Password=Prime@2026";
        Console.WriteLine("ConnectionStrings:DefaultConnection absente — repli localhost:5433.");
    }

    builder.Services.AddLogging(b => b.AddConsole());
    builder.Services.AddDbContext<PrimeDbContext>(o => o.UseNpgsql(conn));
    await using var app = builder.Build();
    var force = args.Contains("--force", StringComparer.OrdinalIgnoreCase);
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
    var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("enrich-demo");
    await db.Database.MigrateAsync();
    await PrimeSchemaPatches.EnsureOrgOptionalAndDraftRootPoleAsync(db);
    var result = await PrimeDbEnrichmentSeeder.EnrichAsync(db, force, CancellationToken.None, log);
    var counts = await PrimeDbEnrichmentSeeder.SnapshotCountsAsync(db);
    Console.WriteLine(result.Applied
        ? $"Enrichissement PRIME v{PrimeDbEnrichmentSeeder.Version} appliqué. Fiches={counts.Fiches}, audit={counts.AuditLogs}, anomalies={counts.Anomalies}, pilotes enrich={counts.EnrichEmployees}"
        : $"Enrichissement ignoré ({result.Reason}). Fiches={counts.Fiches}. Utilisez --force pour réappliquer.");
}

builder.Services.AddControllers().AddJsonOptions(o =>
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
                "http://localhost:4200",
                "http://localhost:4201",
                "http://localhost:4202",
                "http://localhost:4203",
                "http://localhost:4207")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

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
    builder.Services.AddScoped<PrimeRbacReadService>();
    builder.Services.AddScoped<IPrimeRequestUserResolver, PrimeRequestUserResolver>();
    builder.Services.AddScoped<GlobalPoolWorkflowService>();
}

var app = builder.Build();
app.UseCors("devCors");
app.UseMiddleware<ApiExceptionMiddleware>();
app.MapControllers();
app.Run();
