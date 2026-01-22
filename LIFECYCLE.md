# Aspire App Lifecycle Guide

This guide provides a high-level overview of the lifecycle phases of an Aspire application, from development through local deployment to production release. By using the same `AppHost` configuration across all phases, you ensure consistency and reduce configuration drift between environments. This example demonstrates how Aspire orchestrates containerized applications with persistent storage and CI/CD automation using the [Docker Integration](https://aspire.dev/integrations/compute/docker/) and GitHub.

> 📚 For complete Aspire documentation, visit [aspire.dev](https://aspire.dev/)

To run this sample, install the [Aspire prerequisites](https://aspire.dev/get-started/prerequisites/) and the [Aspire CLI](https://aspire.dev/get-started/prerequisites/).

## Overview

The Aspire application lifecycle consists of three main phases:

1. **Inner-Loop Development** - Local development and debugging with `aspire run`
2. **Local Deployment** - Deployment to your defined compute environment(s) with `aspire deploy`. This example shows containerized deployment to Docker Desktop. 
3. **Release (CI/CD)** - Automated build, publish, and deploy pipeline. This example shows using GitHub Actions.

Each phase uses the same `AppHost` configuration but serves different purposes in the development and deployment workflow.

---

## Phase 1: Inner-Loop Development

### What is `aspire run`?

The `aspire run` command starts your Aspire application in **development mode**. This is the inner-loop development experience where you write code, test changes, and debug your application locally.

### How It Works

When you run `aspire run`:

1. **Aspire Dashboard Launches** - A web-based dashboard starts (typically at `http://localhost:18888`)
2. **Resources Start** - All resources defined in your `AppHost.cs` are orchestrated. In this example, they are:
   - SQL Server container starts with persistent volume
   - Blazor Web project runs as a .NET process (not containerized)
   - Database is automatically created and migrated (containerized)
3. **Live Debugging** - You can attach debuggers, set breakpoints, and modify code with hot reload
4. **Telemetry & Logs** - Dashboard provides real-time logs, metrics, and distributed traces

### Running in Development Mode

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

### Development Features

- **Debugging / Hot Reload** - Set breakponts, debug, make changes that apply instantly without restart
- **SQL Server Profiling** - Connect SSMS & database tools to persistent `localhost:1433` with configured password
- **Dashboard Views**:
  - **Resources** - See all services, containers, and projects
  - **Console Logs** - Real-time output from each resource
  - **Structured Logs** - Filtered and structured logging data
  - **Traces** - Distributed tracing across services
  - **Metrics** - Performance metrics and health checks

### Key Configuration in AppHost.cs

```csharp
// Development uses Blazor project directly (not containerized)
var blazorweb = builder.AddProject<Projects.VolumeMount_BlazorWeb>("blazorweb")
    .WithExternalHttpEndpoints()
    .WithReference(sqlDatabase)
    .WaitFor(sqlDatabase)
...

// SQL Server runs in container with persistent volume
var sqlserver = builder.AddSqlServer("sqlserver", password: sqlPassword)
    .WithDataVolume("volumemount-sqlserver-data")
    .WithLifetime(ContainerLifetime.Persistent);
```

### When to Use Development Mode

- Writing and designing new features
- Debugging application issues
- Database schema changes and migrations
- Performance profiling with telemetry
- Integration testing with dependencies

---

## Phase 2: Local Deployment

### What is `aspire deploy`?

The `aspire deploy` command creates a **fully containerized deployment** of your application in the compute environment(s) you define. This simulates a production-like environment on your local machine. In this example, local containers are created on Docker Desktop.

> ⚠️ **Note:** As of January 2026, the `aspire deploy` command is in preview and behavior may change in future releases. Check the latest Aspire CLI documentation for updates.

### How It Works

When you run `aspire deploy`:

1. **Docker Images are Built** - In this example, the .NET project is containerized:
   - Blazor Web app is built into a Docker image and pushed to the GitHub Container Registry
2. **Docker Compose is Generated** - Aspire creates a `docker-compose.yaml` file
3. **Containers Start** - All services run as containers on Docker Desktop:
   - SQL Server container with persistent volume for relational data
   - Blazor Web container with persistent volume for picture uploads storage
   - Aspire Dashboard container (optional, specified by compose parameter in AppHost)
4. **Volumes are Created** - Named volumes persist data between deployments

>⚠️  **Note:** You will need a GitHub personal access token to access the GitHub Container Registry. See [Authenticating to the GitHub Container Registry](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry#authenticating-to-the-container-registry) 

### Running Local Deployment

```bash
# Login to your GitHub Container Registry before deploying
export GITHUB_TOKEN=<YOUR PERSONAL ACCESS TOKEN>
echo $GITHUB_TOKEN | docker login ghcr.io -u USERNAME --password-stdin

# Deploy to Docker Desktop
aspire deploy
```

> ⚠️ **Note:** Aspire will prompt you for parameters the first time you deploy and save them for later use. For more info, see [External Parameters](https://aspire.dev/fundamentals/external-parameters).

The command will:
1. Build container images for projects
2. Generate `docker-compose.yaml` in `./aspire-output/` directory
3. Start all containers using Docker Compose
4. Create and mount persistent volumes

### What Gets Deployed (in this example)

**Containers:**
- `aspire-volumemount-env` - Docker Compose stack 
- `sqlserver` - SQL Server with persistent data volume
- `blazorweb` - Blazor Web app with persistent uploads volume
- `volumemount-env-dashboard` - Monitoring dashboard

**Volumes:**
- `volumemount-sqlserver-data` - Stores database files (`.mdf`, `.ldf`)
- `volumemount-blazor-uploads` - Stores user-uploaded images

### Accessing the Deployed Application

Aspire will display the URLs in the output from the `aspire deploy` command. In this example, you can also access the containers from Docker Desktop.   

1. **View Containers in Docker Desktop** - All services appear in the Containers tab
2. **Access Blazor Web App** - Navigate to `http://localhost:8080` (or configured port)
3. **Access Aspire Dashboard** - Navigate to `http://localhost:18888`  (or configured port) with login token from logs
4. **Connect to SQL Server** - Use SSMS with `localhost:1433` and configured password

### Key Configuration for Deployment

```csharp
// Configure image for deployment
var blazorweb = builder.AddProject<Projects.VolumeMount_BlazorWeb>("blazorweb")
...
    .WithRemoteImageTag("latest")
    .PublishAsDockerComposeService((resource, service) =>
    {
        // Add persistent volume for uploads
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

### When to Use Local Deployment

- Testing containerized application before production
- Verifying Docker Compose configuration
- Testing volume persistence and data retention
- Validating container networking
- Simulating production environment locally

---

## Phase 3: Release (CI/CD)

### What is the GitHub Actions Workflow?

The `.github/workflows/aspire-build-push.yml` workflow automates the build, publish, and deployment process using the **Aspire CLI** in a CI/CD pipeline.

### How This Example Works

The workflow runs on every push to `main` and performs these steps:

1. **Build & Restore** - Compile the Blazor (.NET) web app
2. **Install Aspire CLI** - Install the Aspire CLI
3. **Publish Docker Compose Artifacts** - Generate deployment files with `aspire publish`
4. **Push Container Images** - Build and push images to GitHub Container Registry with `aspire do push`
5. **Upload Artifacts** - Store deployment files for download

### Workflow Steps Explained

#### Step 1: Setup & Build

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

Ensures the Blazor solution compiles successfully before deployment.

#### Step 2: Install Aspire CLI

```yaml
- name: Install Aspire CLI
  run: |
    echo "Installing Aspire CLI from install script..."
    curl -sSL https://aspire.dev/install.sh | bash
```

#### Step 3: Publish Docker Compose Artifacts

```yaml
- name: Prepare Docker Compose with Aspire
  run: |
    aspire publish \
      --project VolumeMount.AppHost/VolumeMount.AppHost.csproj \
      --output-path ./aspire-output
```

**What `aspire publish` does:**
- Analyzes your `AppHost.cs` configuration
- Generates `docker-compose.yaml` file with all service definitions
- Creates `.env` template file for environment variables
- Packages configuration needed for deployment
- Outputs artifacts to `./aspire-output/` directory

**Generated Files:**
```
aspire-output/
├── docker-compose.yaml    # Service definitions for all containers
└── .env                   # Template for required environment variables
```

#### Step 4: Push Container Images to GHCR

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
    Parameters__registry_repository: your-org/your-repo
  run: aspire do push
```

> ⚠️ **Note:** Replace `your-org/your-repo` with your actual GitHub organization and repository name, or use `${{ github.repository_owner }}/${{ github.event.repository.name }}` for automatic values. _Values must be lowercase._

**What `aspire do push` does:**
- Builds Docker container images for projects
- Tags images with configured registry endpoint and repository
- Pushes images to GitHub Container Registry (ghcr.io)
- Uses parameters defined in `AppHost.cs`:
  - `Parameters__registry_endpoint` → `registry-endpoint` parameter
  - `Parameters__registry_repository` → `registry-repository` parameter

**Published Image Example:**
```
ghcr.io/your-org/your-repo/blazorweb:latest
```

#### Step 5: Upload Deployment Artifacts

```yaml
- name: Upload Aspire artifacts
  uses: actions/upload-artifact@v4
  with:
    name: aspire-deployment-files
    path: ./aspire-output/
    retention-days: 30
    include-hidden-files: true
```

In this example, artifacts are available for download from the Actions workflow run for 30 days. Hidden files are included so that the `.env` file is also available in the artifacts.

### Container Registry Configuration in AppHost

```csharp
// Define container registry parameters
var endpoint = builder.AddParameter("registry-endpoint");
var repository = builder.AddParameter("registry-repository");
builder.AddContainerRegistry("container-registry", endpoint, repository);
```

These parameters are provided via environment variables in the workflow:
- `Parameters__registry_endpoint` - Registry URL (e.g., `ghcr.io`)
- `Parameters__registry_repository` - Repository path (e.g., `bethmassi/volumemount`)


### Deploying to Production

After the workflow completes, you have everything needed for production deployment:

1. **Download Artifacts** from GitHub Actions workflow run:
   - `docker-compose.yaml` - Complete service definitions
   - `.env` - Environment variable template

2. **Configure Environment Variables** in `.env`:
   ```bash
   BLAZORWEB_IMAGE=ghcr.io/bethmassi/volumemount/blazorweb:latest 
   BLAZORWEB_PORT=8080
   SQLSERVER_PASSWORD=YourSecurePassword
   ```
   
3. **Login to GitHub Container Registry**
   ```bash
    echo $GITHUB_TOKEN | docker login ghcr.io -u USERNAME --password-stdin
   ```

3. **Deploy with Docker Compose**:
   ```bash
   docker-compose up -d
   ```

4. **Verify Deployment**:
   ```bash
   docker-compose ps
   docker-compose logs -f
   ```

### When to Use CI/CD Release

- Deploying to staging or production environments
- Automating builds and deployments
- Creating versioned container images
- Distributing deployments across multiple servers
- Implementing GitOps workflows

---

## Lifecycle Summary

| Phase | Command | Purpose | Environment | App | Database |
|-------|---------|---------|-------------|------------|------------|
| **Development** | `aspire run` | Inner-loop coding & debugging | Local machine | App process (i.e. .NET) | Container |
| **Local Deploy** | `aspire deploy` | Test containerized app locally | Registered compute environment (i.e. Docker Desktop) | Container | Container |
| **Release** | CI/CD workflow (i.e. GitHub Actions) | Publish to staging/production | Cloud/Server | Container | Container |

## Key Aspire Components 

### AppHost.cs - The Orchestration Center

The `AppHost.cs` file is the single source of truth for your application architecture. It defines:
- **Services & Dependencies** - Projects, containers, and their relationships
- **Configuration** - Connection strings, secrets, and parameters
- **Volumes** - Persistent storage for databases and files
- **Networking** - Endpoints, ports, and service communication
- **Deployment** - Container registry, image tags, and publish settings

### Docker Compose Environment

In this example, Aspire uses Docker Compose as its deployment target, configured via:

```csharp
var compose = builder.AddDockerComposeEnvironment("volumemount-env")
    .WithProperties(env => { env.DashboardEnabled = true; })
    .ConfigureComposeFile(composeFile =>
    {
        composeFile.AddVolume(new Volume
        {
            Name = "volumemount-blazor-uploads",
            Driver = "local"
        });
    });
```
**Benefits:**
- Industry-standard deployment format
- Works with any Docker-compatible platform
- Supports complex multi-container applications
- Enables persistent storage with named volumes
- Simplifies networking between services

The environments your app will be deployed to depends on the compute environment that you register. Compute environments are APIs in the code that implement `IComputeEnvironment`. For more info, see [Compute Environments](https://aspire.dev/deployment/overview/#compute-environments).

### Persistent Volumes

Volumes ensure data survives container restarts and redeployments:

**SQL Server Volume:**
```csharp
var sqlserver = builder.AddSqlServer("sqlserver", password: sqlPassword)
    .WithDataVolume("volumemount-sqlserver-data");
```
- Stores database files (`.mdf`, `.ldf`)
- Mounted at `/var/opt/mssql` inside container
- Persists across deployments

See the [SQL Server Integration](https://aspire.dev/integrations/databases/sql-server/) for more info. 

**Blazor Uploads Volume:**
```csharp
service.AddVolume(new Volume
{
    Name = "volumemount-blazor-uploads",
    Source = "volumemount-blazor-uploads",
    Target = "/app/wwwroot/uploads",
    Type = "volume",
    ReadOnly = false
});
```
- Stores user-uploaded images
- Mounted at `/app/wwwroot/uploads` inside container
- Persists across deployments

---

## Additional Resources

- [Aspire Documentation](https://aspire.dev/)
- [Aspire CLI Reference](https://aspire.dev/reference/aspire-cli/)
- [Aspire Docker Integration](https://aspire.dev/integrations/compute/docker/)
- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [GitHub Container Registry](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)

