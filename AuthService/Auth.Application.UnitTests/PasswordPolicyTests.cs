using Auth.Application.Security;

namespace Auth.Application.UnitTests;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short")]
    [InlineData("Azerty@123")]
    [InlineData("password")]
    public void Rejects_invalid_passwords(string? password)
    {
        Assert.False(PasswordPolicy.TryValidate(password, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Accepts_strong_password()
    {
        Assert.True(PasswordPolicy.TryValidate("Kyntus-Secure-99!", out var error));
        Assert.Equal(string.Empty, error);
    }
}
