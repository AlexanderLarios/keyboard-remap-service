using KeyRemap.Api.Data;
using KeyRemap.Api.Domain;

namespace KeyRemap.Tests;

public class ApexProGen3CatalogTests
{
    [Fact]
    public void Catalog_has_92_keys_with_unique_codes()
    {
        Assert.Equal(92, ApexProGen3Catalog.Keys.Count);
        Assert.Equal(92, ApexProGen3Catalog.Keys.Select(k => k.Code).Distinct().Count());
        Assert.Contains(new HidKey(0x39, "Caps Lock"), ApexProGen3Catalog.Keys);
        Assert.Contains(new HidKey(0xE3, "Left GUI (Win/Cmd)"), ApexProGen3Catalog.Keys);
    }

    [Fact]
    public void Hex_is_two_uppercase_digits()
    {
        Assert.Equal("0x04", new HidKey(0x04, "A").Hex);
        Assert.Equal("0xE0", new HidKey(0xE0, "Left Ctrl").Hex);
    }
}
