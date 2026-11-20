namespace KeyRemap.Api.Domain;

public static class MappingValidator
{
    public static IReadOnlyList<ValidationError> Validate(
        Keyboard keyboard, IReadOnlyList<(int Source, int Target)> entries)
    {
        var keyCodes = keyboard.Keys.Select(k => (int)k.Code).ToHashSet();
        var firstIndexBySource = new Dictionary<int, int>();
        var errors = new List<ValidationError>();

        for (var i = 0; i < entries.Count; i++)
        {
            var (source, target) = entries[i];
            var sourceInRange = CheckRange(i, "source", source, errors);
            var targetInRange = CheckRange(i, "target", target, errors);

            if (sourceInRange)
            {
                CheckOnKeyboard(i, "source", source, keyboard, keyCodes, errors);
                if (!firstIndexBySource.TryAdd(source, i))
                {
                    errors.Add(new ValidationError(i, "source", ValidationError.DuplicateSource,
                        $"{Describe(source)} is already mapped at index {firstIndexBySource[source]}."));
                }
            }

            if (targetInRange)
            {
                CheckOnKeyboard(i, "target", target, keyboard, keyCodes, errors);
            }
        }

        return errors;
    }

    private static bool CheckRange(int index, string field, int value, List<ValidationError> errors)
    {
        if (value is >= 0 and <= 255)
        {
            return true;
        }

        errors.Add(new ValidationError(index, field, ValidationError.OutOfRange,
            $"{value} is not a HID code; codes are 0 to 255."));
        return false;
    }

    private static void CheckOnKeyboard(
        int index, string field, int code, Keyboard keyboard, HashSet<int> keyCodes, List<ValidationError> errors)
    {
        if (!keyCodes.Contains(code))
        {
            errors.Add(new ValidationError(index, field, ValidationError.UnknownKey,
                $"{Describe(code)} is not a key on {keyboard.Name}."));
        }
    }

    private static string Describe(int code) => $"{code} ({HidKey.FormatHex(code)})";
}
