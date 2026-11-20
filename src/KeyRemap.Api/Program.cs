using System.Text.Json.Serialization;
using KeyRemap.Api.Controllers;
using KeyRemap.Api.Data;
using KeyRemap.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

var connectionString = ResolveConnectionString(
    builder.Configuration.GetConnectionString("KeyMappings")
        ?? throw new InvalidOperationException("Connection string 'KeyMappings' is not configured."),
    builder.Environment.ContentRootPath);

// The web defaults accept numbers sent as strings; HID codes must be JSON integers (A3).
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict);
builder.Services.Configure<ApiBehaviorOptions>(options =>
    options.InvalidModelStateResponseFactory = _ => MappingProblems.InvalidBody());
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IKeyboardRepository>(new SqliteKeyboardRepository(connectionString));
builder.Services.AddSingleton<IKeyMappingService, KeyMappingService>();

var app = builder.Build();

DatabaseInitializer.Initialize(connectionString);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();

// A relative path would otherwise resolve against the shell's working directory,
// so starting the service from another folder would open a different database.
static string ResolveConnectionString(string configured, string contentRoot)
{
    var builder = new SqliteConnectionStringBuilder(configured);
    if (!Path.IsPathRooted(builder.DataSource))
    {
        builder.DataSource = Path.Combine(contentRoot, builder.DataSource);
    }

    return builder.ToString();
}
