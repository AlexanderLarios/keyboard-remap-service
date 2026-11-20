using KeyRemap.Api.Domain;

namespace KeyRemap.Api.Contracts;

public sealed record KeyboardMappingsResponse(KeyboardInfo Keyboard, IReadOnlyList<MappingView> Mappings)
{
    public static KeyboardMappingsResponse From(Keyboard keyboard, IReadOnlyList<KeyMapping> mappings) =>
        new(new KeyboardInfo(keyboard.Name),
            mappings.Select(m => new MappingView(KeyView.From(m.Source), KeyView.From(m.Target), m.Remapped)).ToList());
}

public sealed record KeyboardInfo(string Name);

public sealed record MappingView(KeyView Source, KeyView Target, bool Remapped);

public sealed record KeyView(int Code, string Hex, string Name)
{
    public static KeyView From(HidKey key) => new(key.Code, key.Hex, key.Name);
}
