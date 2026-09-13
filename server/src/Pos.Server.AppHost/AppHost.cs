// ReSharper disable StringLiteralTypo
var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Pos_Server_Host>("posserver")
    .WithHttpHealthCheck("/health");

builder.Build().Run();
