using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KeyRemap.Tests;

public sealed class ApiFactory(string databasePath) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:KeyMappings", $"Data Source={databasePath};Foreign Keys=True");
}
