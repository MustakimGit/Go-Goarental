namespace GoGoaRentals.Entities.Enums;

/// <summary>
/// Converts raw database / request values to enums, rejecting values the code does not know about.
/// Scaffolded entities expose TINYINT columns as byte; use these helpers instead of bare casts.
/// </summary>
public static class EnumValue
{
    public static TEnum Parse<TEnum>(byte value) where TEnum : struct, Enum
        => ParseCore<TEnum>(value);

    public static TEnum Parse<TEnum>(short value) where TEnum : struct, Enum
        => ParseCore<TEnum>(value);

    public static bool TryParse<TEnum>(int value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;
        var underlying = Enum.GetUnderlyingType(typeof(TEnum));
        object converted;
        try
        {
            converted = Convert.ChangeType(value, underlying);
        }
        catch (OverflowException)
        {
            return false;
        }

        if (!Enum.IsDefined(typeof(TEnum), converted))
        {
            return false;
        }

        result = (TEnum)Enum.ToObject(typeof(TEnum), converted);
        return true;
    }

    public static byte ToByte<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        EnsureDefined(value);
        return Convert.ToByte(value);
    }

    public static short ToInt16<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        EnsureDefined(value);
        return Convert.ToInt16(value);
    }

    private static TEnum ParseCore<TEnum>(int value) where TEnum : struct, Enum
        => TryParse<TEnum>(value, out var result)
            ? result
            : throw new InvalidEnumValueException(typeof(TEnum), value);

    private static void EnsureDefined<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new InvalidEnumValueException(typeof(TEnum), Convert.ToInt32(value));
        }
    }
}

public sealed class InvalidEnumValueException(Type enumType, int value)
    : Exception($"Value {value} is not a defined {enumType.Name}.")
{
    public Type EnumType { get; } = enumType;

    public int Value { get; } = value;
}
