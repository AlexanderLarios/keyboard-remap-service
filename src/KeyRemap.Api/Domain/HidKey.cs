namespace KeyRemap.Api.Domain;

public sealed record HidKey(byte Code, string Name)
{
    public string Hex => FormatHex(Code);

    public static string FormatHex(int code) => $"0x{code:X2}";
}
