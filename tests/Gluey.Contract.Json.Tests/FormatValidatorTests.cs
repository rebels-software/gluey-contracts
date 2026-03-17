// Copyright 2026 Rebels Software sp. z o.o.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Text;
using Gluey.Contract;
using Gluey.Contract.Json;

namespace Gluey.Contract.Json.Tests;

[TestFixture]
public class FormatValidatorTests
{
    private static ReadOnlySpan<byte> ToBytes(string value) => Encoding.UTF8.GetBytes(value);

    // ── Unknown format ──────────────────────────────────────────────────

    [Test]
    public void Validate_UnknownFormat_ReturnsTrueNoError()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("unknown-format", ToBytes("anything"), "/val", collector)
            .Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    // ── date-time format ────────────────────────────────────────────────

    [Test]
    public void DateTime_ValidUtc_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("date-time", ToBytes("2023-01-15T12:30:00Z"), "/val", collector)
            .Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void DateTime_ValidWithOffset_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("date-time", ToBytes("2023-01-15T12:30:00+05:00"), "/val", collector)
            .Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void DateTime_Invalid_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("date-time", ToBytes("not-a-date"), "/val", collector)
            .Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.FormatInvalid);
    }

    // ── date format ─────────────────────────────────────────────────────

    [Test]
    public void Date_Valid_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("date", ToBytes("2023-01-15"), "/val", collector)
            .Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void Date_InvalidMonth13_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("date", ToBytes("2023-13-01"), "/val", collector)
            .Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.FormatInvalid);
    }

    [Test]
    public void Date_InvalidString_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("date", ToBytes("not-a-date"), "/val", collector)
            .Should().BeFalse();
    }

    // ── time format ─────────────────────────────────────────────────────

    [Test]
    public void Time_ValidWithZ_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("time", ToBytes("14:30:00Z"), "/val", collector)
            .Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void Time_ValidWithOffset_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("time", ToBytes("14:30:00+05:00"), "/val", collector)
            .Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void Time_NoOffset_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("time", ToBytes("14:30:00"), "/val", collector)
            .Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.FormatInvalid);
    }

    [Test]
    public void Time_InvalidHour25_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("time", ToBytes("25:00:00Z"), "/val", collector)
            .Should().BeFalse();
    }

    // ── email format ────────────────────────────────────────────────────

    [Test]
    public void Email_Valid_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("email", ToBytes("user@example.com"), "/val", collector)
            .Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void Email_ValidSimple_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("email", ToBytes("user@host"), "/val", collector)
            .Should().BeTrue();
    }

    [Test]
    public void Email_EmptyLocal_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("email", ToBytes("@example.com"), "/val", collector)
            .Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.FormatInvalid);
    }

    [Test]
    public void Email_EmptyDomain_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("email", ToBytes("user@"), "/val", collector)
            .Should().BeFalse();
    }

    [Test]
    public void Email_NoAtSign_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("email", ToBytes("no-at-sign"), "/val", collector)
            .Should().BeFalse();
    }

    // ── uuid format ─────────────────────────────────────────────────────

    [Test]
    public void Uuid_Valid_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("uuid", ToBytes("550e8400-e29b-41d4-a716-446655440000"), "/val", collector)
            .Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void Uuid_Invalid_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("uuid", ToBytes("not-a-uuid"), "/val", collector)
            .Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.FormatInvalid);
    }

    // ── uri format ──────────────────────────────────────────────────────

    [Test]
    public void Uri_ValidHttps_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("uri", ToBytes("https://example.com"), "/val", collector)
            .Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void Uri_ValidFtp_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("uri", ToBytes("ftp://files.example.com/path"), "/val", collector)
            .Should().BeTrue();
    }

    [Test]
    public void Uri_Invalid_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("uri", ToBytes("not a uri"), "/val", collector)
            .Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.FormatInvalid);
    }

    // ── ipv4 format ─────────────────────────────────────────────────────

    [Test]
    public void Ipv4_Valid_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("ipv4", ToBytes("192.168.1.1"), "/val", collector)
            .Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void Ipv4_OutOfRange_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("ipv4", ToBytes("999.999.999.999"), "/val", collector)
            .Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.FormatInvalid);
    }

    [Test]
    public void Ipv4_NotAnIp_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("ipv4", ToBytes("not-an-ip"), "/val", collector)
            .Should().BeFalse();
    }

    // ── ipv6 format ─────────────────────────────────────────────────────

    [Test]
    public void Ipv6_Loopback_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("ipv6", ToBytes("::1"), "/val", collector)
            .Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void Ipv6_ValidAddress_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("ipv6", ToBytes("2001:db8::1"), "/val", collector)
            .Should().BeTrue();
    }

    [Test]
    public void Ipv6_Invalid_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("ipv6", ToBytes("not-ipv6"), "/val", collector)
            .Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.FormatInvalid);
    }

    // ── json-pointer format ─────────────────────────────────────────────

    [Test]
    public void JsonPointer_EmptyRoot_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("json-pointer", ToBytes(""), "/val", collector)
            .Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void JsonPointer_ValidPath_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("json-pointer", ToBytes("/foo/bar"), "/val", collector)
            .Should().BeTrue();
    }

    [Test]
    public void JsonPointer_ValidWithIndex_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("json-pointer", ToBytes("/foo/0"), "/val", collector)
            .Should().BeTrue();
    }

    [Test]
    public void JsonPointer_NoLeadingSlash_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate("json-pointer", ToBytes("no-leading-slash"), "/val", collector)
            .Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.FormatInvalid);
    }

    // ── Check (zero-allocation) method ──────────────────────────────────

    [TestCase("date-time", "2023-01-15T12:30:00Z", true)]
    [TestCase("date-time", "not-a-date", false)]
    [TestCase("date", "2023-06-15", true)]
    [TestCase("date", "2023-13-01", false)]
    [TestCase("time", "14:30:00Z", true)]
    [TestCase("time", "25:00:00Z", false)]
    [TestCase("email", "user@example.com", true)]
    [TestCase("email", "not-an-email", false)]
    [TestCase("uuid", "550e8400-e29b-41d4-a716-446655440000", true)]
    [TestCase("uuid", "not-a-uuid", false)]
    [TestCase("uri", "https://example.com", true)]
    [TestCase("uri", "not a uri", false)]
    [TestCase("ipv4", "10.0.0.1", true)]
    [TestCase("ipv4", "not-an-ip", false)]
    [TestCase("ipv6", "::1", true)]
    [TestCase("ipv6", "not-ipv6", false)]
    [TestCase("json-pointer", "/foo/bar", true)]
    [TestCase("json-pointer", "no-slash", false)]
    [TestCase("x-custom-format", "anything", true)] // unknown formats pass
    public void Check_ReturnsExpected(string format, string value, bool expected)
    {
        FormatValidator.Check(format, ToBytes(value)).Should().Be(expected);
    }

    // ── Edge cases ──────────────────────────────────────────────────────

    [TestCase("time", "14:30:00.123Z", true)]           // fractional seconds with Z
    [TestCase("time", "14:30:00.123+05:00", true)]      // fractional seconds with offset
    [TestCase("time", "", false)]                        // empty string
    [TestCase("email", "user @example.com", false)]      // spaces
    [TestCase("email", "user@host@domain", false)]       // multiple @ signs
    [TestCase("ipv4", "::1", false)]                     // ipv6 address in ipv4 check
    [TestCase("ipv6", "192.168.1.1", false)]             // ipv4 address in ipv6 check
    [TestCase("json-pointer", "/", true)]                // root slash
    [TestCase("ipv6", "2001:0db8:85a3:0000:0000:8a2e:0370:7334", true)] // full ipv6
    public void Validate_EdgeCase_ReturnsExpected(string format, string value, bool expected)
    {
        using var collector = new ErrorCollector();
        FormatValidator.Validate(format, ToBytes(value), "/val", collector)
            .Should().Be(expected);
    }

    // ── Validate method error reporting ─────────────────────────────

    [Test]
    public void Validate_UnknownFormat_NoError()
    {
        using var collector = new ErrorCollector();
        bool result = FormatValidator.Validate("x-unknown", ToBytes("anything"), "/val", collector);

        result.Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void Validate_InvalidIpv4_AddsFormatError()
    {
        using var collector = new ErrorCollector();
        bool result = FormatValidator.Validate("ipv4", ToBytes("not-ip"), "/addr", collector);

        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.FormatInvalid);
        collector[0].Path.Should().Be("/addr");
    }

    // ── Check with all valid formats ────────────────────────────────

    [TestCase("date-time", "2026-03-17T10:30:00-05:00", true)]
    [TestCase("date", "2000-01-01", true)]
    [TestCase("date", "2000-02-29", true)]   // leap year
    [TestCase("date", "2001-02-29", false)]  // not a leap year
    [TestCase("time", "00:00:00Z", true)]
    [TestCase("time", "23:59:59Z", true)]
    [TestCase("time", "23:59:59.9999999Z", true)]
    [TestCase("email", "a@b", true)]
    [TestCase("email", "", false)]
    [TestCase("uuid", "00000000-0000-0000-0000-000000000000", true)]
    [TestCase("uri", "mailto:user@example.com", true)]
    [TestCase("uri", "urn:isbn:0451450523", true)]
    [TestCase("ipv4", "0.0.0.0", true)]
    [TestCase("ipv4", "255.255.255.255", true)]
    [TestCase("ipv6", "fe80::1", true)]
    [TestCase("json-pointer", "/a/b/c", true)]
    [TestCase("json-pointer", "/0", true)]
    public void Check_AdditionalCases(string format, string value, bool expected)
    {
        FormatValidator.Check(format, ToBytes(value)).Should().Be(expected);
    }
}
