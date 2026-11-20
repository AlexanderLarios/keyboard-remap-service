using KeyRemap.Api.Data;
using KeyRemap.Api.Domain;

namespace KeyRemap.Tests;

public class MappingValidatorTests
{
    private static readonly Keyboard Kb = new(1, ApexProGen3Catalog.Name, ApexProGen3Catalog.Keys);

    [Fact]
    public void Swap_and_shared_target_pass() =>
        Assert.Empty(MappingValidator.Validate(Kb, [(4, 29), (29, 4), (57, 224), (33, 31), (32, 31)]));

    [Fact]
    public void Self_map_passes() =>
        Assert.Empty(MappingValidator.Validate(Kb, [(4, 4)]));

    [Theory]
    [InlineData(45, 4, "source")]
    [InlineData(4, 45, "target")]
    public void Code_not_on_keyboard_is_unknown_key(int source, int target, string field)
    {
        var error = Assert.Single(MappingValidator.Validate(Kb, [(source, target)]));
        Assert.Equal(
            ((int?)0, field, "unknown_key", "45 (0x2D) is not a key on Apex Pro Gen 3."),
            (error.Index, error.Field, error.Code, error.Message));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    public void Out_of_range_reports_once(int bad)
    {
        var error = Assert.Single(MappingValidator.Validate(Kb, [(bad, 4)]));
        Assert.Equal(("source", "out_of_range"), (error.Field, error.Code));
    }

    [Fact]
    public void Repeated_source_names_both_indexes()
    {
        var error = Assert.Single(MappingValidator.Validate(Kb, [(4, 29), (5, 6), (6, 7), (4, 30)]));
        Assert.Equal(
            ((int?)3, "source", "duplicate_source", "4 (0x04) is already mapped at index 0."),
            (error.Index, error.Field, error.Code, error.Message));
    }

    [Fact]
    public void Three_mistakes_give_three_errors() =>
        Assert.Equal(
            ["out_of_range", "unknown_key", "duplicate_source"],
            MappingValidator.Validate(Kb, [(4, 300), (45, 4), (5, 6), (5, 7)]).Select(e => e.Code));
}
