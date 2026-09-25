namespace Pos.Domain.Logic;

public sealed class PinHasherTests
{
    // 同じ PIN でもソルトで別のハッシュになり、元の PIN だけが照合に通る
    [Fact]
    public void HashAndVerify()
    {
        // Arrange
        var first = PinHasher.Hash("1234");
        var second = PinHasher.Hash("1234");

        // Act
        var matched = PinHasher.Verify("1234", first);
        var wrong = PinHasher.Verify("1235", first);

        // Assert
        Assert.NotEqual(first, second);
        Assert.True(matched);
        Assert.False(wrong);
        Assert.True(PinHasher.Verify("1234", second));
    }

    // 未設定・壊れたハッシュは照合に通らない
    [Fact]
    public void VerifyRejectsMissingHash()
    {
        // Act
        var missing = PinHasher.Verify("1234", null);
        var broken = PinHasher.Verify("1234", [1, 2, 3]);

        // Assert
        Assert.False(missing);
        Assert.False(broken);
    }

    // 4〜6 桁の数字だけ
    [Theory]
    [InlineData("1234", true)]
    [InlineData("123456", true)]
    [InlineData("123", false)]
    [InlineData("1234567", false)]
    [InlineData("12a4", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidFormat(string? pin, bool expected)
    {
        // Act
        var valid = PinHasher.IsValidFormat(pin);

        // Assert
        Assert.Equal(expected, valid);
    }
}
