using Microsoft.EntityFrameworkCore;
using PlanningService.Data;
using PlanningService.Interfaces;
using PlanningService.Services;
using PlanningService.Hubs;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

using PlanningServiceImpl = PlanningService.Services.PlanningService;

var builder = WebApplication.CreateBuilder(args);

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:4200",
                "http://localhost:80",
                "http://localhost"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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

                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();


// Services
builder.Services.AddScoped<IFloorService, FloorService>();
builder.Services.AddScoped<IServiceService, ServiceService>();
builder.Services.AddScoped<ISubServiceService, SubServiceService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IPlanningService, PlanningServiceImpl>();
builder.Services.AddScoped<IReclamationService, ReclamationService>();
builder.Services.AddScoped<IPropositionService, PropositionService>();
builder.Services.AddScoped<IReclamationNotificationService, ReclamationNotificationService>();
// ? NOUVEAU — Newsletter
builder.Services.AddScoped<INewsletterService, NewsletterService>();

builder.Services.AddSignalR();

// Controllers
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

// Migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var maxRetries = 10;
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            db.Database.Migrate();
            Console.WriteLine("? Migrations appliquées avec succès.");
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Attente DB... tentative {i + 1}/{maxRetries}: {ex.Message}");
            Thread.Sleep(3000);
        }
    }
}

// Seed Shifts
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

// Sync employés
using (var scope = app.Services.CreateScope())
{
    var planningService = scope.ServiceProvider.GetRequiredService<IPlanningService>();
    await planningService.SyncNewEmployeesAsync();
}

// ?? Middleware pipeline ??????????????????????????????
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

// ? NOUVEAU — Hub Newsletter
app.MapHub<NewsletterHub>("/hubs/newsletter");
app.MapHub<ReclamationHub>("/hubs/reclamation");
app.Run();