# Aspire Sample: Blazor Monolith w/ SQL Database, Persistent Volumes & GHCR Image Publish Workflow

_I just started learning Aspire and wanted to see how it could used for a simple Blazor web app with SQL database. It was initially created from the [aspire-samples/volumemount](https://github.com/dotnet/aspire-samples/tree/main/samples/volume-mount) sample, but I wanted to also explore how the Docker Integration worked as well as how I could use the aspire CLI in an Actions Workflow to create and publish the image in GitHub Container Registry. Many thanks to captiansafia for her help!_

To run this sample, install the [Aspire prerequisites](https://aspire.dev/get-started/prerequisites/) and the [Aspire CLI](https://aspire.dev/get-started/prerequisites/).

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

## GitHub Actions Workflow - Aspire CLI Integration

The `.github/workflows/aspire-build-push.yml` workflow demonstrates automated deployment using the Aspire CLI:

### Workflow Steps

#### 1. Build and Restore
```yaml
- name: Setup .NET
  uses: actions/setup-dotnet@v4
  with:
    dotnet-version: '10.0.x'

- name: Restore dependencies
  run: dotnet restore

- name: Build solution
  run: dotnet build --configuration Release --no-restore
```

#### 2. Install Aspire CLI
```yaml
- name: Install Aspire CLI
  run: dotnet tool install --global aspire.cli --prerelease
```

The Aspire CLI is a global .NET tool that provides commands for publishing and deploying Aspire applications.

#### 3. Publish Docker Compose Artifacts
```yaml
- name: Prepare Docker Compose with Aspire
  run: |
    aspire publish \
      --project VolumeMount.AppHost/VolumeMount.AppHost.csproj \
      --output-path ./aspire-output
```

**What `aspire publish` does:**
- Generates a `docker-compose.yaml` file based on the AppHost configuration
- Creates an `.env` template file for environment variables
- Packages all configuration needed to deploy the application
- Outputs artifacts to `./aspire-output/` directory

**Generated Files:**
- `docker-compose.yaml` - Complete service definitions for all containers
- `.env` - Template for required environment variables (passwords, ports, etc.)

#### 4. Push Container Image to GitHub Container Registry
```yaml
- name: Login to GHCR
  uses: docker/login-action@v3
  with:
    registry: ghcr.io
    username: ${{ github.actor }}
    password: ${{ secrets.GITHUB_TOKEN }}

- name: Push images with Aspire
  env:
    Parameters__registry_endpoint: ghcr.io
    Parameters__registry_repository: bethmassi/volumemount
  run: aspire do push
```

**What `aspire do push` does:**
- Builds the Docker container image for the BlazorWeb project
- Tags the image with the configured registry endpoint and repository
- Pushes the image to GitHub Container Registry (ghcr.io)
- Uses parameters defined in the AppHost configuration

**Environment Variables:**
- `Parameters__registry_endpoint` - Maps to the `registry-endpoint` parameter in AppHost
- `Parameters__registry_repository` - Maps to the `registry-repository` parameter in AppHost

### Deployment Artifacts

After the workflow runs, you get:
1. **Docker Compose files** uploaded as GitHub Actions artifacts (retained for 30 days)
2. **Container image** published to GitHub Container Registry at `ghcr.io/bethmassi/volumemount/blazorweb:latest`

These can be used to deploy the application to any Docker-compatible environment:
```bash
# Download the docker-compose.yaml and .env files
# Set required environment variables in .env file
docker-compose up -d
```

## Local Development

To run the sample locally:

1. Install [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
2. Install <a href="https://www.docker.com/products/docker-desktop">Docker Desktop</a>
3. Install <a href="https://aspire.dev/get-started/install-cli/">Aspire CLI</a>
4. Set the SQL Server password as a user secret:
   ```bash
   dotnet user-secrets set "Parameters:sqlserver-password" "YourSecurePassword123!" --project VolumeMount.AppHost
   ```
5. Run the AppHost:
   ```bash
   cd VolumeMount.AppHost
   aspire run
   ```
6. Open the Aspire Dashboard (URL shown in console output)
7. Access the BlazorWeb application through the dashboard

## Local Deployment

To containerize and deploy the application locally:

```bash
aspire deploy
```

This will set up the containerized application and volume mounts on your Docker desktop.

**Running the Aspire Dashboard:**
- The dashboard will be available at `http://localhost:18888` (default port)
- You will need to provide the login parameter in the URL. To view this key, open the Logs for the container.
- You can view logs, metrics, and traces for all services
- Navigate to the services tab to see all running containers

**Running the BlazorWeb app:**
- Once deployed, the BlazorWeb application will be available in docker desktop. 
- Access it at `http://localhost:8080` or the configured port
- The app connects to the SQL Server container automatically via the configured connection string

## Production Deployment

To deploy to production using the published artifacts:

1. Download the `docker-compose.yaml` and `.env` files from the Aspire publish output. These are the artifacts you can download as an archive (.zip) from the Actions workflow run.
2. Configure environment variables in the `.env` file:
   ```
   SQLSERVER_PASSWORD=YourSecurePassword
   BLAZORWEB_IMAGE=ghcr.io/bethmassi/volumemount/blazorweb:latest
   BLAZORWEB_PORT=8080
   ```
3. Deploy using Docker Compose:
   ```bash
   docker-compose up -d
   ```

## Key Takeaways

- **Aspire simplifies container orchestration** - Configure everything in C# with strong typing
- **Persistent volumes preserve data** - Both database and uploaded files survive container restarts
- **Aspire CLI enables GitOps** - Generate deployment artifacts and publish images in CI/CD pipelines using the aspire CLI commands `aspire publish` and `aspire do push`.
- **Flexible deployment options** - Same configuration works for local development and production
