using KeyRemap.Api.Data;
using KeyRemap.Api.Domain;

namespace KeyRemap.Api.Services;

public sealed class KeyMappingService(IKeyboardRepository repository) : IKeyMappingService
{
    private static readonly MappingResult NotFound = new(MappingOutcome.KeyboardNotFound);

    public async Task<MappingResult> GetMappingsAsync(string keyboardName, CancellationToken ct)
    {
        var keyboard = await repository.FindKeyboardAsync(keyboardName, ct);
        if (keyboard is null)
        {
            return NotFound;
        }

        var mappings = await repository.GetMappingsAsync(keyboard.Id, ct);
        return new MappingResult(MappingOutcome.Success, keyboard, mappings);
    }

    public async Task<MappingResult> AssignMappingsAsync(
        string keyboardName, IReadOnlyList<(int Source, int Target)> entries, CancellationToken ct)
    {
        var keyboard = await repository.FindKeyboardAsync(keyboardName, ct);
        if (keyboard is null)
        {
            return NotFound;
        }

        var errors = MappingValidator.Validate(keyboard, entries);
        if (errors.Count > 0)
        {
            return new MappingResult(MappingOutcome.Invalid, keyboard, Errors: errors);
        }

        var keysByCode = keyboard.Keys.ToDictionary(k => (int)k.Code);
        var mappings = entries.Select(e => new KeyMapping(keysByCode[e.Source], keysByCode[e.Target])).ToList();
        await repository.UpsertMappingsAsync(keyboard.Id, mappings, ct);
        return new MappingResult(MappingOutcome.Success, keyboard);
    }

    public async Task<MappingResult> ResetMappingsAsync(string keyboardName, CancellationToken ct)
    {
        var keyboard = await repository.FindKeyboardAsync(keyboardName, ct);
        if (keyboard is null)
        {
            return NotFound;
        }

        await repository.ClearMappingsAsync(keyboard.Id, ct);
        return new MappingResult(MappingOutcome.Success, keyboard);
    }
}
