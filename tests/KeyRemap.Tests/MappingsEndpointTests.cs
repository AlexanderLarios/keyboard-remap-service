using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace KeyRemap.Tests;

public class MappingsEndpointTests : ApiTestBase
{
    [Fact]
    public async Task Second_patch_with_other_key_keeps_first_two()
    {
        await PatchAsync((4, 29), (33, 31));
        await PatchAsync((5, 6));

        var mappings = await GetMappingsAsync();
        Assert.Equal(29, TargetOf(mappings, 4));
        Assert.Equal(31, TargetOf(mappings, 33));
        Assert.Equal(6, TargetOf(mappings, 5));
    }

    [Fact]
    public async Task Patch_changes_existing_target()
    {
        await PatchAsync((4, 29));
        await PatchAsync((4, 30));

        Assert.Equal(30, TargetOf(await GetMappingsAsync(), 4));
    }

    [Fact]
    public async Task Self_map_resets_one_key()
    {
        await PatchAsync((4, 29), (33, 31));
        var response = await PatchAsync((4, 4));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var mappings = await GetMappingsAsync();
        Assert.False(IsRemapped(mappings, 4));
        Assert.Equal(4, TargetOf(mappings, 4));
        Assert.True(IsRemapped(mappings, 33));
    }

    [Fact]
    public async Task Remapping_one_key_repeatedly_keeps_one_row()
    {
        await PatchAsync((4, 29));
        await PatchAsync((4, 30));
        await PatchAsync((4, 4));

        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        await connection.OpenAsync();
        await using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM key_mappings WHERE source_code = 4";
        Assert.Equal(1L, (long)(await count.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Sending_get_result_back_changes_nothing()
    {
        await PatchAsync((4, 29));
        var before = await GetMappingsAsync();

        var all = before.EnumerateArray()
            .Select(m => (m.GetProperty("source").GetProperty("code").GetInt32(),
                          m.GetProperty("target").GetProperty("code").GetInt32()))
            .ToArray();
        var response = await PatchAsync(all);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(before.GetRawText(), (await GetMappingsAsync()).GetRawText());
    }

    [Fact]
    public async Task Empty_patch_changes_nothing()
    {
        await PatchAsync((4, 29));
        var response = await PatchAsync();

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(IsRemapped(await GetMappingsAsync(), 4));
    }

    [Fact]
    public async Task Delete_resets_every_key()
    {
        await PatchAsync((4, 29), (33, 31));
        var response = await Client.DeleteAsync(Url);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var mappings = await GetMappingsAsync();
        Assert.Equal(92, mappings.GetArrayLength());
        Assert.All(mappings.EnumerateArray(), m => Assert.False(m.GetProperty("remapped").GetBoolean()));
    }

    [Fact]
    public async Task Invalid_patch_leaves_state_unchanged()
    {
        await PatchAsync((4, 29));
        var response = await PatchAsync((5, 6), (45, 4));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var mappings = await GetMappingsAsync();
        Assert.True(IsRemapped(mappings, 4));
        Assert.False(IsRemapped(mappings, 5));
    }

    [Fact]
    public async Task Name_is_matched_ignoring_case()
    {
        var body = await Client.GetFromJsonAsync<JsonElement>("/api/keyboards/apex%20pro%20gen%203/mappings");

        Assert.Equal("Apex Pro Gen 3", body.GetProperty("keyboard").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Unknown_name_returns_404_on_all_verbs()
    {
        const string unknown = "/api/keyboards/Nope/mappings";

        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync(unknown)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Client.PatchAsJsonAsync(unknown, new { mappings = Array.Empty<object>() })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.DeleteAsync(unknown)).StatusCode);
    }

    [Fact]
    public async Task Mappings_survive_restart()
    {
        await PatchAsync((4, 29));

        Restart();

        var mappings = await GetMappingsAsync();
        Assert.Equal(92, mappings.GetArrayLength());
        Assert.Equal(29, TargetOf(mappings, 4));
    }
}
