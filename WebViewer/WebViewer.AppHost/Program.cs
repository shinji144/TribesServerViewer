var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var db = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("dbtest");

var apiService = builder.AddProject<Projects.WebViewer_ApiService>("apiservice");

builder.AddProject<Projects.WebViewer_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(cache)
    .WithReference(apiService);

builder.Build().Run();
