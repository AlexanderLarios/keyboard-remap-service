namespace KeyRemap.Api.Contracts;

public sealed class AssignMappingsRequest
{
    public required List<MappingEntry> Mappings { get; init; }
}
