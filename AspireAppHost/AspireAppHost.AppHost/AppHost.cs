var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.WebApiMediatorCQRS>("webapimediatorcqrs");

builder.Build().Run();
