using Doctorly.Domain.Exceptions;
using Doctorly.Domain.ValueObjects;

namespace Doctorly.Domain.Tests.ValueObjects;

public class EmailAddressTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    [InlineData("@missing-local.com")]
    public void Constructor_InvalidValue_Throws(string value)
    {
        Assert.Throws<DomainException>(() => new EmailAddress(value));
    }

    [Fact]
    public void Constructor_TooLong_Throws()
    {
        var value = new string('a', EmailAddress.MaxLength) + "@example.com";

        Assert.Throws<DomainException>(() => new EmailAddress(value));
    }

    [Fact]
    public void Constructor_ValidValue_Succeeds()
    {
        var email = new EmailAddress("patient@example.com");

        Assert.Equal("patient@example.com", email.Value);
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var a = new EmailAddress("patient@example.com");
        var b = new EmailAddress("patient@example.com");

        Assert.Equal(a, b);
    }
}
