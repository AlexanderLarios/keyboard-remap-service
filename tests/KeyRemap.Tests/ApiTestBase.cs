using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace KeyRemap.Tests;

public abstract class ApiTestBase : IDisposable
{
    protected const string Url = "/api/keyboards/Apex%20Pro%20Gen%203/mappings";

    protected readonly string DatabasePath = Path.Combine(Path.GetTempPath(), $"keyremap-{Guid.NewGuid():N}.db");

    protected ApiTestBase()
    {
        Factory = new ApiFactory(DatabasePath);
        Client = Factory.CreateClient();
    }

    protected ApiFactory Factory { get; private set; }

    protected HttpClient Client { get; private set; }

    protected void Restart()
    {
        Client.Dispose();
        Factory.Dispose();
        Factory = new ApiFactory(DatabasePath);
        Client = Factory.CreateClient();
    }

    protected Task<HttpResponseMessage> PatchAsync(params (int Source, int Target)[] mappings) =>
        Client.PatchAsJsonAsync(Url, new { mappings = mappings.Select(m => new { source = m.Source, target = m.Target }) });

    protected Task<HttpResponseMessage> PatchRawAsync(string json, string mediaType = "application/json") =>
        Client.PatchAsync(Url, new StringContent(json, System.Text.Encoding.UTF8, mediaType));

    protected async Task<JsonElement> GetMappingsAsync(string url = Url)
    {
        var body = await Client.GetFromJsonAsync<JsonElement>(url);
        return body.GetProperty("mappings");
    }

    protected static JsonElement Entry(JsonElement mappings, int source) =>
        mappings.EnumerateArray().Single(m => m.GetProperty("source").GetProperty("code").GetInt32() == source);

    protected static int TargetOf(JsonElement mappings, int source) =>
        Entry(mappings, source).GetProperty("target").GetProperty("code").GetInt32();

    protected static bool IsRemapped(JsonElement mappings, int source) =>
        Entry(mappings, source).GetProperty("remapped").GetBoolean();

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
        SqliteConnection.ClearAllPools();
        File.Delete(DatabasePath);
        GC.SuppressFinalize(this);
    }
}
