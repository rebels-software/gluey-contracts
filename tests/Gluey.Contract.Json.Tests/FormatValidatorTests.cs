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
}
