var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.AuthService>("authservice");

builder.Build().Run();
