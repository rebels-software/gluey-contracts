// Copyright 2025 Rebels Software sp. z o.o.
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

namespace Gluey.Contract.Tests;

[TestFixture]
public class OffsetTableTests
{
    [Test]
    public void Created_WithCapacity_HasCorrectCount()
    {
        using var table = new OffsetTable(8);
        table.Count.Should().Be(8);
    }

    [Test]
    public void Set_AndRetrieve_ReturnsSameValue()
    {
        var buffer = Encoding.UTF8.GetBytes("hello");
        var prop = new ParsedProperty(buffer, 0, buffer.Length, "/name");

        using var table = new OffsetTable(4);
        table.Set(2, prop);

        table[2].HasValue.Should().BeTrue();
        table[2].GetString().Should().Be("hello");
        table[2].Path.Should().Be("/name");
    }

    [Test]
    public void Indexer_OutOfRange_ReturnsEmpty()
    {
        using var table = new OffsetTable(4);

        table[10].HasValue.Should().BeFalse();
        table[-1].HasValue.Should().BeFalse();
    }

    [Test]
    public void Indexer_UnsetOrdinal_ReturnsEmpty()
    {
        using var table = new OffsetTable(4);

        table[0].HasValue.Should().BeFalse();
        table[3].HasValue.Should().BeFalse();
    }

    [Test]
    public void Dispose_DoesNotThrow()
    {
        var table = new OffsetTable(4);
        var act = () => table.Dispose();
        act.Should().NotThrow();
    }

    [Test]
    public void Default_HasCountZero()
    {
        var table = default(OffsetTable);
        table.Count.Should().Be(0);
    }

    [Test]
    public void Default_Dispose_DoesNotThrow()
    {
        var table = default(OffsetTable);
        var act = () => table.Dispose();
        act.Should().NotThrow();
    }

    [Test]
    public void IsReadonlyStruct_ImplementsIDisposable()
    {
        typeof(OffsetTable).IsValueType.Should().BeTrue();
        typeof(IDisposable).IsAssignableFrom(typeof(OffsetTable)).Should().BeTrue();
    }

    [Test]
    public void DoubleDispose_DoesNotThrow()
    {
        var table = new OffsetTable(4);

        var act = () =>
        {
            table.Dispose();
            table.Dispose();
        };

        act.Should().NotThrow();
    }

    [Test]
    public void Set_MultipleOrdinals_AllRetrievable()
    {
        var buf1 = Encoding.UTF8.GetBytes("Alice");
        var buf2 = Encoding.UTF8.GetBytes("42");

        var prop1 = new ParsedProperty(buf1, 0, buf1.Length, "/name");
        var prop2 = new ParsedProperty(buf2, 0, buf2.Length, "/age");

        using var table = new OffsetTable(4);
        table.Set(0, prop1);
        table.Set(1, prop2);

        table[0].GetString().Should().Be("Alice");
        table[1].GetInt32().Should().Be(42);
    }
}
