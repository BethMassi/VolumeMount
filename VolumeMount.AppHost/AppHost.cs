using System.Net.Sockets;
using Aspire.Hosting;
using Aspire.Hosting.Docker;
using Aspire.Hosting.Docker.Resources.ServiceNodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = DistributedApplication.CreateBuilder(args);

// Enable verbose logging for Docker operations
builder.Services.AddLogging(logging =>
{
    logging.SetMinimumLevel(LogLevel.Debug);
});

// Add Docker Compose environment
var compose = builder.AddDockerComposeEnvironment("volumemount-env")
    .WithProperties(env =>
{
    env.DashboardEnabled = true;
});

if (!builder.Environment.IsDevelopment())
{
    compose.ConfigureComposeFile(composeFile =>
     {
         // Add the blazor-uploads volume to the top-level volumes section
         composeFile.AddVolume(new Volume
         {
             Name = "volumemount-blazor-uploads",
             Driver = "local"
         });
     });
}

var sqlPassword = builder.AddParameter("sqlserver-password", secret: true);
var sqlserver = builder.AddSqlServer("sqlserver", password: sqlPassword)
    .WithDataVolume("volumemount-sqlserver-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithHostPort(1433)
    .WithEndpoint("tcp", e =>
    //This allows us to connect SSMS to the database even if aspire isn't running,
    //in both development "aspire run" and production "aspire deploy" scenarios.
    {
        e.Port = 1433;
        e.TargetPort = 1433;
        e.Protocol = ProtocolType.Tcp;
        e.IsProxied = false;
        e.IsExternal = true;
    });   

var sqlDatabase = sqlserver.AddDatabase("sqldb");

var blazorweb = builder.AddProject<Projects.VolumeMount_BlazorWeb>("blazorweb")
        .WithExternalHttpEndpoints()
        .WithReference(sqlDatabase)
        .WaitFor(sqlDatabase);

//Deploy the Blazor Web project as a Docker Compose service with a volume mount for uploads
if (!builder.Environment.IsDevelopment())
{
    blazorweb.PublishAsDockerComposeService((resource, service) =>
    {
        service.AddVolume(new Volume
        {
            Name = "volumemount-blazor-uploads",
            Source = "volumemount-blazor-uploads",
            Target = "/app/wwwroot/uploads",
            Type = "volume",
            ReadOnly = false
        });
        // Run container as root initially to fix permissions
        service.User = "root";

        // Override the entrypoint to fix permissions so users can write (upload pictures)
        // to the volume then run the default entrypoint as app user
        service.Command = new List<string>
        {
            "/bin/sh",
            "-c",
            "chown -R app:app /app/wwwroot/uploads && chmod -R 755 /app/wwwroot/uploads && exec su app -c 'dotnet /app/VolumeMount.BlazorWeb.dll'"
        };

    });
}

builder.Build().Run();
