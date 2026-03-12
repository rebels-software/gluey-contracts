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

namespace Gluey.Contract.Json.Tests;

[TestFixture]
internal sealed class JsonByteReaderTests
{
    private static byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);

    [Test]
    public void Read_SimpleObject_ProducesCorrectTokenSequence()
    {
        var json = Utf8("""{"name":"alice","age":30}""");
        var reader = new JsonByteReader(json);
        var tokens = new List<JsonByteTokenType>();

        while (reader.Read())
            tokens.Add(reader.TokenType);

        tokens.Should().Equal(
            JsonByteTokenType.StartObject,
            JsonByteTokenType.PropertyName,
            JsonByteTokenType.String,
            JsonByteTokenType.PropertyName,
            JsonByteTokenType.Number,
            JsonByteTokenType.EndObject);
    }

    [Test]
    public void Read_StringPropertyName_OffsetPointsInsideQuotes()
    {
        // {"name":"alice"}
        // 0123456789...
        // PropertyName "name" starts at byte 1 (opening quote), content at byte 2
        var json = Utf8("""{"name":"alice"}""");
        var reader = new JsonByteReader(json);

        reader.Read(); // StartObject
        reader.Read(); // PropertyName "name"

        reader.TokenType.Should().Be(JsonByteTokenType.PropertyName);
        reader.ByteOffset.Should().Be(2); // TokenStartIndex(1) + 1
        reader.ByteLength.Should().Be(4); // "name" is 4 bytes
    }

    [Test]
    public void Read_StringValue_OffsetPointsInsideQuotes()
    {
        // {"name":"alice"}
        var json = Utf8("""{"name":"alice"}""");
        var reader = new JsonByteReader(json);

        reader.Read(); // StartObject
        reader.Read(); // PropertyName "name"
        reader.Read(); // String "alice"

        reader.TokenType.Should().Be(JsonByteTokenType.String);
        reader.ByteOffset.Should().Be(9); // TokenStartIndex(8) + 1
        reader.ByteLength.Should().Be(5); // "alice" is 5 bytes
    }

    [Test]
    public void Read_NumberToken_OffsetPointsToStart()
    {
        // {"age":30}
        // 0123456789
        var json = Utf8("""{"age":30}""");
        var reader = new JsonByteReader(json);

        reader.Read(); // StartObject
        reader.Read(); // PropertyName "age"
        reader.Read(); // Number 30

        reader.TokenType.Should().Be(JsonByteTokenType.Number);
        reader.ByteOffset.Should().Be(7); // TokenStartIndex
        reader.ByteLength.Should().Be(2); // "30" is 2 bytes
    }

    [Test]
    public void Read_BooleanTrue_ProducesTrueTokenType()
    {
        var json = Utf8("""{"ok":true}""");
        var reader = new JsonByteReader(json);

        reader.Read(); // StartObject
        reader.Read(); // PropertyName
        reader.Read(); // True

        reader.TokenType.Should().Be(JsonByteTokenType.True);
        reader.ByteLength.Should().Be(4); // "true" is 4 bytes
    }

    [Test]
    public void Read_BooleanFalse_ProducesFalseTokenType()
    {
        var json = Utf8("""{"ok":false}""");
        var reader = new JsonByteReader(json);

        reader.Read(); // StartObject
        reader.Read(); // PropertyName
        reader.Read(); // False

        reader.TokenType.Should().Be(JsonByteTokenType.False);
        reader.ByteLength.Should().Be(5); // "false" is 5 bytes
    }

    [Test]
    public void Read_NullValue_ProducesNullTokenType()
    {
        var json = Utf8("""{"v":null}""");
        var reader = new JsonByteReader(json);

        reader.Read(); // StartObject
        reader.Read(); // PropertyName
        reader.Read(); // Null

        reader.TokenType.Should().Be(JsonByteTokenType.Null);
        reader.ByteLength.Should().Be(4); // "null" is 4 bytes
    }

    [Test]
    public void Read_EmptyObject_ProducesStartAndEndObject()
    {
        var json = Utf8("{}");
        var reader = new JsonByteReader(json);
        var tokens = new List<JsonByteTokenType>();

        while (reader.Read())
            tokens.Add(reader.TokenType);

        tokens.Should().Equal(
            JsonByteTokenType.StartObject,
            JsonByteTokenType.EndObject);
    }

    [Test]
    public void Read_StructuralTokens_ReportCorrectOffsets()
    {
        // { }
        // 0 1
        var json = Utf8("{}");
        var reader = new JsonByteReader(json);

        reader.Read(); // StartObject
        reader.ByteOffset.Should().Be(0);
        reader.ByteLength.Should().Be(1);

        reader.Read(); // EndObject
        reader.ByteOffset.Should().Be(1);
        reader.ByteLength.Should().Be(1);
    }

    [Test]
    public void Read_NestedObjectsAndArrays_TokenizesCorrectly()
    {
        var json = Utf8("""{"a":{"b":[1,2]}}""");
        var reader = new JsonByteReader(json);
        var tokens = new List<JsonByteTokenType>();

        while (reader.Read())
            tokens.Add(reader.TokenType);

        tokens.Should().Equal(
            JsonByteTokenType.StartObject,
            JsonByteTokenType.PropertyName,   // "a"
            JsonByteTokenType.StartObject,
            JsonByteTokenType.PropertyName,   // "b"
            JsonByteTokenType.StartArray,
            JsonByteTokenType.Number,         // 1
            JsonByteTokenType.Number,         // 2
            JsonByteTokenType.EndArray,
            JsonByteTokenType.EndObject,
            JsonByteTokenType.EndObject);
    }

    [Test]
    public void Read_ByteArrayInput_Works()
    {
        byte[] json = Utf8("""{"x":1}""");
        var reader = new JsonByteReader(json);
        var tokens = new List<JsonByteTokenType>();

        while (reader.Read())
            tokens.Add(reader.TokenType);

        tokens.Should().HaveCount(4); // StartObject, PropertyName, Number, EndObject
    }

    [Test]
    public void Read_ReadOnlyMemoryInput_Works()
    {
        ReadOnlyMemory<byte> json = Utf8("""{"x":1}""");
        var reader = new JsonByteReader(json.Span);
        var tokens = new List<JsonByteTokenType>();

        while (reader.Read())
            tokens.Add(reader.TokenType);

        tokens.Should().HaveCount(4);
    }

    [Test]
    public void Read_MismatchedBraces_SetsHasError()
    {
        var json = Utf8("""{"a": 1]""");
        var reader = new JsonByteReader(json);

        while (reader.Read()) { }

        reader.HasError.Should().BeTrue();
        reader.Error.Kind.Should().Be(JsonReadErrorKind.InvalidJson);
    }

    [Test]
    public void Read_InvalidToken_SetsHasError()
    {
        var json = Utf8("{nope}");
        var reader = new JsonByteReader(json);

        while (reader.Read()) { }

        reader.HasError.Should().BeTrue();
    }

    [Test]
    public void Read_TruncatedInput_SetsUnexpectedEndOfData()
    {
        var json = Utf8("""{"a":""");
        var reader = new JsonByteReader(json);

        while (reader.Read()) { }

        reader.HasError.Should().BeTrue();
        reader.Error.Kind.Should().Be(JsonReadErrorKind.UnexpectedEndOfData);
    }

    [Test]
    public void Read_Error_HasNonNegativeByteOffset()
    {
        var json = Utf8("""{"a": 1]""");
        var reader = new JsonByteReader(json);

        while (reader.Read()) { }

        reader.HasError.Should().BeTrue();
        reader.Error.ByteOffset.Should().BeGreaterThanOrEqualTo(0);
    }

    [Test]
    public void Read_Error_HasNonEmptyMessage()
    {
        var json = Utf8("""{"a": 1]""");
        var reader = new JsonByteReader(json);

        while (reader.Read()) { }

        reader.HasError.Should().BeTrue();
        reader.Error.Message.Should().NotBeNullOrEmpty();
    }
}
