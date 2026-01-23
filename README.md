# Aspire Sample: Blazor Web w/ SQL Database, Persistent Volumes & GHCR Image Publish Workflow

_This sample was initially created from the [aspire-samples/volumemount](https://github.com/dotnet/aspire-samples/tree/main/samples/volume-mount) sample. It uses the [Docker Integration](https://aspire.dev/integrations/compute/docker/) and the [Aspire CLI](https://aspire.dev/get-started/install-cli/) in an Actions Workflow to create and publish the container image into GitHub Container Registry, and produces Docker Compose artifacts._

> 📖 **New!** [Read the Aspire App Lifecycle Guide](LIFECYCLE.md) - Learn about the complete development → local deploy → release workflow using this sample.

This sample demonstrates how to use <a href="https://aspire.dev">Aspire</a> to orchestrate a multi-container application with persistent data storage and automated deployment to production. It showcases:

- **Persistent SQL Server volumes** - Database data persists across container restarts
- **Persistent file storage volumes** - User-uploaded images persist across container restarts
- **Aspire Docker Integration** - Seamless configuration and orchestration of containerized services
- **GitHub Actions CI/CD** - Automated building, publishing, and deploying using the Aspire CLI
- **Identity management** - ASP.NET Core Identity with SQL Server backend
- **Blazor Server UI** - Interactive web application for file uploads

## What This Sample Demonstrates

### Aspire AppHost Configuration

The `AppHost.cs` file in the `VolumeMount.AppHost` project is the heart of the Aspire orchestration. It demonstrates:

#### 1. Docker Compose Environment Setup
```csharp
var compose = builder.AddDockerComposeEnvironment("volumemount-env")
    .WithProperties(env => { env.DashboardEnabled = true; })
    .ConfigureComposeFile(composeFile =>
    {
        // Define top-level volumes for sharing across services
        composeFile.AddVolume(new Volume
        {
            Name = "volumemount-blazor-uploads",
            Driver = "local"
        });
    });
```

#### 2. Container Registry Configuration
```csharp
var endpoint = builder.AddParameter("registry-endpoint");
var repository = builder.AddParameter("registry-repository");
builder.AddContainerRegistry("container-registry", endpoint, repository);
```

This configures the container registry (GitHub Container Registry in this case) where the BlazorWeb container image will be pushed during deployment.

#### 3. SQL Server with Persistent Volume
```csharp
var sqlPassword = builder.AddParameter("sqlserver-password", secret: true);
var sqlserver = builder.AddSqlServer("sqlserver", password: sqlPassword)
    .WithDataVolume("volumemount-sqlserver-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithHostPort(1433)
    .WithEndpoint("tcp", e =>
    {
        e.Port = 1433;
        e.TargetPort = 1433;
        e.Protocol = ProtocolType.Tcp;
        e.IsProxied = false;
        e.IsExternal = true;
    });
```

**Key Features:**
- `WithDataVolume()` - Mounts a named volume to `/var/opt/mssql` to persist database files
- `WithLifetime(ContainerLifetime.Persistent)` - Container stays running across app restarts
- `WithEndpoint()` with `IsProxied = false` and `IsExternal = true` - Allows direct connection from SQL Server Management Studio (SSMS) even when Aspire isn't running

#### 4. Blazor Web Application with File Upload Volume
```csharp
var blazorweb = builder.AddProject<Projects.VolumeMount_BlazorWeb>("blazorweb")
    .WithExternalHttpEndpoints()
    .WithReference(sqlDatabase)
    .WaitFor(sqlDatabase)
    .WithRemoteImageTag("latest")
    .PublishAsDockerComposeService((resource, service) =>
    {
        // Mount volume for persistent file uploads
        service.AddVolume(new Volume
        {
            Name = "volumemount-blazor-uploads",
            Source = "volumemount-blazor-uploads",
            Target = "/app/wwwroot/uploads",
            Type = "volume",
            ReadOnly = false
        });
        
        // Configure permissions for file uploads
        service.User = "root";
        service.Command = new List<string>
        {
            "/bin/sh", "-c",
            "chown -R app:app /app/wwwroot/uploads && chmod -R 755 /app/wwwroot/uploads && exec su app -c 'dotnet /app/VolumeMount.BlazorWeb.dll'"
        };
    });
```

**Key Features:**
- `WithRemoteImageTag("latest")` - Tags the container image for pushing to registry
- `PublishAsDockerComposeService()` - Customizes the Docker Compose service definition
- Volume mounting to `/app/wwwroot/uploads` - Persists user-uploaded images
- Permission management - Ensures the app user can write to the uploads directory

### Persistent Volumes Explained

#### SQL Server Volume (`volumemount-sqlserver-data`)
- **Purpose:** Stores SQL Server database files (`.mdf`, `.ldf`)
- **Mount Point:** `/var/opt/mssql` inside the container
- **Benefits:** 
  - Database persists across container restarts
  - No data loss when redeploying applications
  - Allows database upgrades without data migration

#### Blazor Uploads Volume (`volumemount-blazor-uploads`)
- **Purpose:** Stores user-uploaded image files
- **Mount Point:** `/app/wwwroot/uploads` inside the container
- **Benefits:**
  - Uploaded images persist across container restarts
  - Multiple container instances can share the same upload storage
  - Files remain available after application updates

## Running this Sample

To run this sample, first install the [Aspire prerequisites](https://aspire.dev/get-started/prerequisites/) and the [Aspire CLI](https://aspire.dev/get-started/install-cli/).

```bash
# Navigate to the project root folder
cd VolumeMount

# Start the application
aspire run
```

Aspire will auto-detect and run the `AppHost`. The console will display the dashboard URL with a login token:
```
info: Aspire.Hosting.DistributedApplication[0]
      Aspire version: 13.1.0
      Dashboard: http://localhost:18888/login?t=abc123xyz...
```

Alternately, you can use an editor like Visual Studio Code to start debugging via the `AppHost`.

Open the dashboard link and click the `blazorweb` resource URL to launch the app. 

## The Aspire App Lifecycle

Aspire not only helps you set up, develop, and locally run distributed apps, it also helps you with local and production deployment and workflows. **[Read the Aspire App Lifecycle Guide](LIFECYCLE.md) to learn about the complete development → local deploy → release workflow using this sample.**