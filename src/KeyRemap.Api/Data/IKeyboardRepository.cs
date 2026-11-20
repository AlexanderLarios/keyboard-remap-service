using KeyRemap.Api.Domain;

namespace KeyRemap.Api.Data;

/// <summary>Stores keyboards, their keys and the mappings set for each key.</summary>
public interface IKeyboardRepository
{
    /// <summary>Finds a keyboard by name, ignoring case, with its keys ordered by HID code.</summary>
    /// <returns>The keyboard with its stored name, or <c>null</c> if there is none.</returns>
    Task<Keyboard?> FindKeyboardAsync(string name, CancellationToken ct);

    /// <summary>Returns one mapping per key on the keyboard, ordered by source code. Keys with no stored mapping map to themselves.</summary>
    Task<IReadOnlyList<KeyMapping>> GetMappingsAsync(int keyboardId, CancellationToken ct);

    /// <summary>Inserts or overwrites the stored mapping for each source key, all in one transaction.</summary>
    Task UpsertMappingsAsync(int keyboardId, IReadOnlyList<KeyMapping> mappings, CancellationToken ct);

    /// <summary>Removes every stored mapping for the keyboard.</summary>
    Task ClearMappingsAsync(int keyboardId, CancellationToken ct);
}
