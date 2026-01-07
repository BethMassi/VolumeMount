using Microsoft.AspNetCore.Identity;
using VolumeMount.BlazorWeb.Components;
using VolumeMount.BlazorWeb.Components.Account;
using VolumeMount.BlazorWeb.Data;
using VolumeMount.BlazorWeb.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

//builder.AddAzureBlobServiceClient("BlobConnection");
builder.AddSqlServerDbContext<ApplicationDbContext>("sqldb");
// Print connection string to console
//var connectionString = builder.Configuration.GetConnectionString("sqldb");
//Console.WriteLine($"Database connection string: {connectionString}");


// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();
builder.Services.AddAuthorization();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// Register database initializer
builder.Services.AddScoped<DatabaseInitializer>();

// Register photo upload configuration
builder.Services.Configure<PhotoUploadConfiguration>(
    builder.Configuration.GetSection(PhotoUploadConfiguration.SectionName));

// Register photo upload service
builder.Services.AddScoped<IPhotoUploadService, PhotoUploadService>();
builder.Services.AddScoped<IPhotoDeleteService, PhotoDeleteService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// initialize the database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbInitializer = services.GetRequiredService<DatabaseInitializer>();
    await dbInitializer.InitializeDatabaseAsync();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.MapDefaultEndpoints();

//app.MapPost("api/upload", async (HttpRequest request) =>
//{
//    if (!request.HasFormContentType)
//    {
//        return Results.BadRequest("Invalid form content type.");
//    }
//    var form = await request.ReadFormAsync();
//    var file = form.Files["file"];
//    if (file == null || file.Length == 0)
//    {
//        return Results.BadRequest("No file uploaded.");
//    }
//    // Use environment-aware path: local folder for dev, container volume for production
//    var uploadsFolder = app.Environment.IsDevelopment()
//        ? Path.Combine(Directory.GetCurrentDirectory(), "uploads")
//        : "/uploads";
//    if (!Directory.Exists(uploadsFolder))
//    {
//        Directory.CreateDirectory(uploadsFolder);
//    }
//    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
//    var filePath = Path.Combine(uploadsFolder, fileName);
//    using (var stream = new FileStream(filePath, FileMode.Create))
//    {
//        await file.CopyToAsync(stream);
//    }
//    return Results.Ok(new { file.FileName, file.Length });
//});
app.MapGet("api/listfiles", () =>
{
    // Use environment-aware path: local folder for dev, container volume for production
    var uploadsFolder = app.Environment.IsDevelopment()
        ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads")
        : "/uploads";
    if (!Directory.Exists(uploadsFolder))
    {
        return Results.Ok(new List<string>());
    }
    var files = Directory.GetFiles(uploadsFolder)
        .Select(f => Path.GetFileName(f))
        .ToList();
    return Results.Ok(files);
}); 
app.Run();
