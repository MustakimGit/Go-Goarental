using GoGoaRentals.Entities.Enums;

namespace GoGoaRentals.Tests.Unit.Enums;

public class EnumValueTests
{
    [Fact]
    public void Parse_returns_enum_for_defined_value()
    {
        Assert.Equal(BookingStatus.Confirmed, EnumValue.Parse<BookingStatus>((byte)4));
        Assert.Equal(NotificationEventType.PartnerBookingAssigned, EnumValue.Parse<NotificationEventType>((short)200));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(255)]
    public void Parse_throws_for_unknown_value(byte value)
    {
        var ex = Assert.Throws<InvalidEnumValueException>(() => EnumValue.Parse<BookingStatus>(value));
        Assert.Equal(typeof(BookingStatus), ex.EnumType);
        Assert.Equal(value, ex.Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    [InlineData(int.MaxValue)]
    public void TryParse_rejects_values_outside_underlying_range(int value)
    {
        Assert.False(EnumValue.TryParse<BookingStatus>(value, out _));
    }

    [Fact]
    public void ToByte_throws_for_undefined_enum_value()
    {
        Assert.Throws<InvalidEnumValueException>(() => EnumValue.ToByte((PaymentStatus)99));
    }

    [Fact]
    public void All_persisted_enums_start_at_one_so_default_is_never_a_valid_state()
    {
        var enumTypes = typeof(EnumValue).Assembly.GetTypes().Where(t => t.IsEnum);
        foreach (var type in enumTypes)
        {
            Assert.False(Enum.IsDefined(type, Enum.ToObject(type, 0)), $"{type.Name} defines 0");
        }
    }
}
