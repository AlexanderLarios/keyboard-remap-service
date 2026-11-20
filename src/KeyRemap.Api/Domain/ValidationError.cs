namespace KeyRemap.Api.Domain;

public sealed record ValidationError(int? Index, string Field, string Code, string Message)
{
    public const string InvalidBody = "invalid_body";
    public const string OutOfRange = "out_of_range";
    public const string UnknownKey = "unknown_key";
    public const string DuplicateSource = "duplicate_source";
}
