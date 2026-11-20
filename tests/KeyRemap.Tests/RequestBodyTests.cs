using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace KeyRemap.Tests;

public class RequestBodyTests : ApiTestBase
{
    [Fact]
    public async Task Get_on_fresh_database_returns_92_unmapped_keys()
    {
        var body = await Client.GetFromJsonAsync<JsonElement>(Url);

        Assert.Equal("Apex Pro Gen 3", body.GetProperty("keyboard").GetProperty("name").GetString());
        var mappings = body.GetProperty("mappings");
        Assert.Equal(92, mappings.GetArrayLength());
        Assert.All(mappings.EnumerateArray(), m => Assert.False(m.GetProperty("remapped").GetBoolean()));
        Assert.Equal("0x04", mappings[0].GetProperty("source").GetProperty("hex").GetString());
        Assert.Equal("A", mappings[0].GetProperty("target").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Patch_two_keys_then_get_shows_A_to_Z_and_4_to_2()
    {
        var response = await PatchAsync((4, 29), (33, 31));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var mappings = await GetMappingsAsync();
        Assert.Equal(29, TargetOf(mappings, 4));
        Assert.Equal(31, TargetOf(mappings, 33));
        Assert.True(IsRemapped(mappings, 4));
        Assert.True(IsRemapped(mappings, 33));
        Assert.Equal("Z", Entry(mappings, 4).GetProperty("target").GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"mappings\":null}")]
    [InlineData("{\"mappings\":[{\"source\":4}]}")]
    [InlineData("{\"mappings\":[{\"source\":\"4\",\"target\":29}]}")]
    [InlineData("{\"mappings\":[{\"source\":4.5,\"target\":29}]}")]
    [InlineData("{\"mappings\":[{\"source\":null,\"target\":29}]}")]
    [InlineData("{\"mappings\":[{\"source\":99999999999,\"target\":29}]}")]
    [InlineData("not json")]
    [InlineData("{\"mappings\":[null]}")]
    public async Task Malformed_body_is_invalid_body(string json)
    {
        var response = await PatchRawAsync(json);

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("One or more mappings are invalid.", problem.GetProperty("title").GetString());
        var error = Assert.Single(problem.GetProperty("errors").EnumerateArray());
        Assert.Equal("invalid_body", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Invalid_entries_return_problem_with_errors_array()
    {
        var response = await PatchAsync((4, 29), (45, 4));

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.1", problem.GetProperty("type").GetString());
        var error = Assert.Single(problem.GetProperty("errors").EnumerateArray());
        Assert.Equal(1, error.GetProperty("index").GetInt32());
        Assert.Equal("source", error.GetProperty("field").GetString());
        Assert.Equal("unknown_key", error.GetProperty("code").GetString());
        Assert.Equal("45 (0x2D) is not a key on Apex Pro Gen 3.", error.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Body_without_json_content_type_returns_415()
    {
        var response = await PatchRawAsync("{\"mappings\":[]}", "text/plain");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_keyboard_returns_404_problem()
    {
        var response = await Client.GetAsync("/api/keyboards/Nope/mappings");

        var problem = await AssertProblemAsync(response, HttpStatusCode.NotFound);
        Assert.Equal("Keyboard not found.", problem.GetProperty("title").GetString());
    }

    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
