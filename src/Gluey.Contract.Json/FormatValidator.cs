using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Gluey.Contract;

namespace Gluey.Contract.Json;

/// <summary>
/// Validates JSON values against format keywords defined in JSON Schema Draft 2020-12.
/// Supports 9 standard formats: date-time, date, time, email, uuid, uri, ipv4, ipv6, json-pointer.
/// </summary>
internal static class FormatValidator
{
    /// <summary>
    /// Validates a value against the specified format string.
    /// Unknown formats pass silently (return <c>true</c>).
    /// </summary>
    /// <param name="format">The format keyword value (e.g. "date-time", "email").</param>
    /// <param name="valueBytes">The raw UTF-8 bytes of the value to validate.</param>
    /// <param name="path">The JSON Pointer path for error reporting.</param>
    /// <param name="collector">The error collector to receive any validation errors.</param>
    /// <returns><c>true</c> if the value matches the format or the format is unknown; otherwise <c>false</c>.</returns>
    internal static bool Validate(string format, ReadOnlySpan<byte> valueBytes, string path, ErrorCollector collector)
    {
        bool valid = format switch
        {
            "date-time" => ValidateDateTime(valueBytes),
            "date" => ValidateDate(valueBytes),
            "time" => ValidateTime(valueBytes),
            "email" => ValidateEmail(valueBytes),
            "uuid" => ValidateUuid(valueBytes),
            "uri" => ValidateUri(valueBytes),
            "ipv4" => ValidateIpv4(valueBytes),
            "ipv6" => ValidateIpv6(valueBytes),
            "json-pointer" => ValidateJsonPointer(valueBytes),
            _ => true // Unknown formats pass silently
        };

        if (!valid)
        {
            collector.Add(new ValidationError(
                path,
                ValidationErrorCode.FormatInvalid,
                ValidationErrorMessages.Get(ValidationErrorCode.FormatInvalid)));
        }

        return valid;
    }

    private static string BytesToString(ReadOnlySpan<byte> bytes) => Encoding.UTF8.GetString(bytes);

    private static bool ValidateDateTime(ReadOnlySpan<byte> bytes)
    {
        var value = BytesToString(bytes);
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    private static bool ValidateDate(ReadOnlySpan<byte> bytes)
    {
        var value = BytesToString(bytes);
        return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    private static bool ValidateTime(ReadOnlySpan<byte> bytes)
    {
        var value = BytesToString(bytes);

        // RFC 3339 requires offset indicator (Z, +, or -)
        if (value.Length == 0)
            return false;

        // Must have offset: Z or +HH:MM or -HH:MM
        bool hasOffset = value.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
            || (value.Length > 6 && (value[^6] == '+' || value[^6] == '-'));

        if (!hasOffset)
            return false;

        // Try parsing the time portion
        // Strip offset for TimeOnly parsing
        string timePart;
        if (value.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
        {
            timePart = value[..^1];
        }
        else
        {
            // +HH:MM or -HH:MM (6 chars)
            timePart = value[..^6];
        }

        return TimeOnly.TryParseExact(timePart, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            || TimeOnly.TryParseExact(timePart, "HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    private static bool ValidateEmail(ReadOnlySpan<byte> bytes)
    {
        var value = BytesToString(bytes);

        // Simplified structural check: exactly one @, non-empty local, non-empty domain, no spaces
        if (value.Contains(' '))
            return false;

        int atIndex = value.IndexOf('@');
        if (atIndex < 0)
            return false;

        // Must have exactly one @
        if (value.LastIndexOf('@') != atIndex)
            return false;

        string local = value[..atIndex];
        string domain = value[(atIndex + 1)..];

        return local.Length > 0 && domain.Length > 0;
    }

    private static bool ValidateUuid(ReadOnlySpan<byte> bytes)
    {
        var value = BytesToString(bytes);
        return Guid.TryParse(value, out _);
    }

    private static bool ValidateUri(ReadOnlySpan<byte> bytes)
    {
        var value = BytesToString(bytes);
        return Uri.TryCreate(value, UriKind.Absolute, out _);
    }

    private static bool ValidateIpv4(ReadOnlySpan<byte> bytes)
    {
        var value = BytesToString(bytes);
        return IPAddress.TryParse(value, out var addr) && addr.AddressFamily == AddressFamily.InterNetwork;
    }

    private static bool ValidateIpv6(ReadOnlySpan<byte> bytes)
    {
        var value = BytesToString(bytes);
        return IPAddress.TryParse(value, out var addr) && addr.AddressFamily == AddressFamily.InterNetworkV6;
    }

    private static bool ValidateJsonPointer(ReadOnlySpan<byte> bytes)
    {
        var value = BytesToString(bytes);

        // Empty string is valid root pointer
        if (value.Length == 0)
            return true;

        // Non-empty must start with /
        return value[0] == '/';
    }
}
