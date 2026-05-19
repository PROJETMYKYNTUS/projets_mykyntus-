using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

builder.Services.AddCors(options => {
    options.AddPolicy("AllowAngular", policy => {
        policy.WithOrigins(
            "http://localhost:4200",  // planning-frontend
            "http://localhost:4201",  // auth-frontend
            "http://localhost:4202"   // prime-frontend
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();
app.UseCors("AllowAngular");
app.UseWebSockets();
await app.UseOcelot();
app.Run();