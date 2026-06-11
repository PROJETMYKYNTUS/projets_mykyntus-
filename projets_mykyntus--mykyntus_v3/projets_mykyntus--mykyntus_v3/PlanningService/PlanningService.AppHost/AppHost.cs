var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.PlanningService>("planningservice");

builder.Build().Run();
