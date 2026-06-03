using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Planning.Messaging.Publishers;
using PlanningService.Data;
using PlanningService.Hubs;
using PlanningService.Interfaces;
using PlanningService.Messaging.Consumers;
using PlanningService.Messaging.Publishers;
using PlanningService.Services;
using System.Text;
using System.Text.Json.Serialization;

using PlanningServiceImpl = PlanningService.Services.PlanningService;

var builder = WebApplication.CreateBuilder(args);

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:8200",
                "http://localhost:80",
                "http://localhost"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── JWT ───────────────────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["JwtSettings:Secret"]
    ?? throw new InvalidOperationException("JwtSettings:Secret manquant");

var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "AuthService";
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "AuthServiceClient";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── Services existants ────────────────────────────────────────────────────────
builder.Services.AddScoped<IFloorService, FloorService>();
builder.Services.AddScoped<IServiceService, ServiceService>();
builder.Services.AddScoped<ISubServiceService, SubServiceService>();
builder.Services.AddHttpClient<IUserService, UserService>(client =>
{
    client.BaseAddress = new Uri("http://kyntus_auth_backend:8080/");
    client.Timeout = TimeSpan.FromSeconds(10); 
});
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IPlanningService, PlanningServiceImpl>();
builder.Services.AddScoped<IReclamationService, ReclamationService>();
builder.Services.AddScoped<IPropositionService, PropositionService>();
builder.Services.AddScoped<IReclamationNotificationService, ReclamationNotificationService>();
builder.Services.AddScoped<INewsletterService, NewsletterService>();

// ── 🆕 MassTransit + RabbitMQ ─────────────────────────────────────────────────
builder.Services.AddScoped<IEmployePublisher, EmployePublisher>();

builder.Services.AddMassTransit(x =>
{
    // Consumer : reçoit les congés validés depuis Conge Service
    x.AddConsumer<CongeValideConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        // Queue : écoute les congés validés depuis Conge Service
        cfg.ReceiveEndpoint("planning-conge-valide", e =>
        {
            e.ConfigureConsumer<CongeValideConsumer>(ctx);
        });

        cfg.ConfigureEndpoints(ctx);
    });
});

// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── Controllers ───────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        opts.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var app = builder.Build();

// ── Migrations ────────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var maxRetries = 10;
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            db.Database.Migrate();
            Console.WriteLine("✅ Migrations appliquées avec succès.");
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⏳ Attente DB... tentative {i + 1}/{maxRetries}: {ex.Message}");
            Thread.Sleep(3000);
        }
    }
}

// ── Seed Shifts ───────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!context.Shifts.Any())
    {
        context.Shifts.AddRange(
            new PlanningService.Models.Shift { Label = "8h", StartTime = new TimeOnly(8, 0), LunchBreakTime = new TimeOnly(12, 0) },
            new PlanningService.Models.Shift { Label = "9h", StartTime = new TimeOnly(9, 0), LunchBreakTime = new TimeOnly(13, 0) },
            new PlanningService.Models.Shift { Label = "10h", StartTime = new TimeOnly(10, 0), LunchBreakTime = new TimeOnly(14, 0) },
            new PlanningService.Models.Shift { Label = "11h", StartTime = new TimeOnly(11, 0), LunchBreakTime = new TimeOnly(15, 0) }
        );
        await context.SaveChangesAsync();
    }
}

// ── Sync employés ─────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var planningService = scope.ServiceProvider.GetRequiredService<IPlanningService>();
    await planningService.SyncNewEmployeesAsync();
}

using (var scope = app.Services.CreateScope())
{
    var planningDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DockerComposePlanningDemoSeed.ApplyIfEnabledAsync(app.Configuration, planningDb);
}
// 🆕 Re-sync des users sans AuthUserId
using (var scope = app.Services.CreateScope())
{
    var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
    await userService.SyncMissingAuthUsersAsync();
}

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngular");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapHub<PlanningHub>("/hubs/planning");
app.MapHub<NewsletterHub>("/hubs/newsletter");
app.MapHub<ReclamationHub>("/hubs/reclamation");

app.Run();
