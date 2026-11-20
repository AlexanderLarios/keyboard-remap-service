namespace KeyRemap.Api.Domain;

public sealed record KeyMapping(HidKey Source, HidKey Target)
{
    public bool Remapped => Source.Code != Target.Code;
}
