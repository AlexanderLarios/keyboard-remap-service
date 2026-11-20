using KeyRemap.Api.Contracts;
using KeyRemap.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace KeyRemap.Api.Controllers;

[ApiController]
[Route("api/keyboards/{keyboardName}/mappings")]
public sealed class KeyboardMappingsController(IKeyMappingService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<KeyboardMappingsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string keyboardName, CancellationToken ct)
    {
        var result = await service.GetMappingsAsync(keyboardName, ct);
        return result.Outcome == MappingOutcome.Success
            ? Ok(KeyboardMappingsResponse.From(result.Keyboard!, result.Mappings!))
            : ToProblem(result, keyboardName);
    }

    [HttpPatch]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Patch(string keyboardName, AssignMappingsRequest request, CancellationToken ct)
    {
        var entries = request.Mappings.Select(m => (m.Source, m.Target)).ToList();
        var result = await service.AssignMappingsAsync(keyboardName, entries, ct);
        return result.Outcome == MappingOutcome.Success ? NoContent() : ToProblem(result, keyboardName);
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string keyboardName, CancellationToken ct)
    {
        var result = await service.ResetMappingsAsync(keyboardName, ct);
        return result.Outcome == MappingOutcome.Success ? NoContent() : ToProblem(result, keyboardName);
    }

    private IActionResult ToProblem(MappingResult result, string keyboardName) => result.Outcome switch
    {
        MappingOutcome.Invalid => MappingProblems.Invalid(result.Errors!),
        _ => Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Keyboard not found.",
            detail: $"No keyboard is named '{keyboardName}'."),
    };
}
