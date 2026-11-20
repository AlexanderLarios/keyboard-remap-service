namespace KeyRemap.Api.Domain;

public sealed record Keyboard(int Id, string Name, IReadOnlyList<HidKey> Keys);
