using System.Text.RegularExpressions;
using Doctorly.Domain.Exceptions;

namespace Doctorly.Domain.ValueObjects;

public sealed partial record EmailAddress
{
    public const int MaxLength = 254;

    public string Value { get; }

    public EmailAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Email address is required.");

        if (value.Length > MaxLength)
            throw new DomainException($"Email address must be {MaxLength} characters or fewer.");

        if (!EmailRegex().IsMatch(value))
            throw new DomainException($"'{value}' is not a valid email address.");

        Value = value;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
