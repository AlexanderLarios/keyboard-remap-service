using KeyRemap.Api.Data;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

var connectionString = ResolveConnectionString(
    builder.Configuration.GetConnectionString("KeyMappings")
        ?? throw new InvalidOperationException("Connection string 'KeyMappings' is not configured."),
    builder.Environment.ContentRootPath);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IKeyboardRepository>(new SqliteKeyboardRepository(connectionString));

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
