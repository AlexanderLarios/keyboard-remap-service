namespace KeyRemap.Api.Contracts;

public sealed class MappingEntry
{
    // int rather than byte so that 256 reaches the validator as out_of_range instead of failing binding.
    public required int Source { get; init; }

    public required int Target { get; init; }
}
