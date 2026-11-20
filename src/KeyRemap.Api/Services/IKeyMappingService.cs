using KeyRemap.Api.Domain;

namespace KeyRemap.Api.Services;

/// <summary>Reads, assigns and resets the key mappings of a keyboard identified by name.</summary>
public interface IKeyMappingService
{
    /// <summary>Returns every key on the keyboard with the code it currently emits.</summary>
    Task<MappingResult> GetMappingsAsync(string keyboardName, CancellationToken ct);

    /// <summary>
    /// Validates the entries and, if all are valid, stores them on top of the current mappings.
    /// Keys not listed keep their mapping; a key mapped to itself returns to its default.
    /// </summary>
    Task<MappingResult> AssignMappingsAsync(
        string keyboardName, IReadOnlyList<(int Source, int Target)> entries, CancellationToken ct);

    /// <summary>Returns every key on the keyboard to its default.</summary>
    Task<MappingResult> ResetMappingsAsync(string keyboardName, CancellationToken ct);
}

public enum MappingOutcome
{
    Success,
    KeyboardNotFound,
    Invalid,
}

public sealed record MappingResult(
    MappingOutcome Outcome,
    Keyboard? Keyboard = null,
    IReadOnlyList<KeyMapping>? Mappings = null,
    IReadOnlyList<ValidationError>? Errors = null);
