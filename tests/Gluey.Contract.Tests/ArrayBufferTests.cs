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

using Gluey.Contract;

namespace Gluey.Contract.Tests;

[TestFixture]
public class ArrayBufferTests
{
    [Test]
    public void Rent_ReturnsNonNull()
    {
        var buffer = ArrayBuffer.Rent(16, 4);
        buffer.Should().NotBeNull();
        buffer.Dispose();
    }

    [Test]
    public void Add_And_Get_ReturnsElement()
    {
        var buffer = ArrayBuffer.Rent(16, 4);

        var prop = new ParsedProperty(new byte[] { 1, 2, 3 }, 0, 3, "/0");
        buffer.Add(0, prop);

        var result = buffer.Get(0, 0);
        result.HasValue.Should().BeTrue();
        buffer.Dispose();
    }

    [Test]
    public void Get_InvalidOrdinal_ReturnsEmpty()
    {
        var buffer = ArrayBuffer.Rent(16, 4);

        buffer.Get(-1, 0).HasValue.Should().BeFalse();
        buffer.Get(999, 0).HasValue.Should().BeFalse();
        buffer.Dispose();
    }

    [Test]
    public void Get_InvalidIndex_ReturnsEmpty()
    {
        var buffer = ArrayBuffer.Rent(16, 4);
        var prop = new ParsedProperty(new byte[] { 1 }, 0, 1, "/0");
        buffer.Add(0, prop);

        buffer.Get(0, -1).HasValue.Should().BeFalse();
        buffer.Get(0, 1).HasValue.Should().BeFalse(); // only index 0 exists
        buffer.Dispose();
    }

    [Test]
    public void Get_UnsetOrdinal_ReturnsEmpty()
    {
        var buffer = ArrayBuffer.Rent(16, 4);

        buffer.Get(0, 0).HasValue.Should().BeFalse();
        buffer.Dispose();
    }

    [Test]
    public void GetCount_ReturnsCorrectCount()
    {
        var buffer = ArrayBuffer.Rent(16, 4);
        var prop = new ParsedProperty(new byte[] { 1 }, 0, 1, "/0");
        buffer.Add(0, prop);
        buffer.Add(0, prop);
        buffer.Add(0, prop);

        buffer.GetCount(0).Should().Be(3);
        buffer.GetCount(1).Should().Be(0);
        buffer.Dispose();
    }

    [Test]
    public void GetCount_InvalidOrdinal_ReturnsZero()
    {
        var buffer = ArrayBuffer.Rent(16, 4);

        buffer.GetCount(-1).Should().Be(0);
        buffer.GetCount(999).Should().Be(0);
        buffer.Dispose();
    }

    [Test]
    public void Add_MultipleOrdinals_TracksIndependently()
    {
        var buffer = ArrayBuffer.Rent(16, 4);
        var propA = new ParsedProperty(new byte[] { 1 }, 0, 1, "/a/0");
        var propB = new ParsedProperty(new byte[] { 2 }, 0, 1, "/b/0");

        buffer.Add(0, propA);
        buffer.Add(0, propA);
        buffer.Add(1, propB);

        buffer.GetCount(0).Should().Be(2);
        buffer.GetCount(1).Should().Be(1);
        buffer.Get(0, 0).HasValue.Should().BeTrue();
        buffer.Get(1, 0).HasValue.Should().BeTrue();
        buffer.Dispose();
    }

    [Test]
    public void Add_ExceedsCapacity_GrowsAutomatically()
    {
        var buffer = ArrayBuffer.Rent(2, 4); // small initial capacity
        var prop = new ParsedProperty(new byte[] { 1 }, 0, 1, "/0");

        for (int i = 0; i < 10; i++)
            buffer.Add(0, prop);

        buffer.GetCount(0).Should().Be(10);
        buffer.Get(0, 9).HasValue.Should().BeTrue();
        buffer.Dispose();
    }

    [Test]
    public void Add_ExceedsRegionCapacity_GrowsAutomatically()
    {
        var buffer = ArrayBuffer.Rent(16, 2); // small region capacity
        var prop = new ParsedProperty(new byte[] { 1 }, 0, 1, "/0");

        buffer.Add(10, prop); // ordinal exceeds initial region capacity

        buffer.GetCount(10).Should().Be(1);
        buffer.Get(10, 0).HasValue.Should().BeTrue();
        buffer.Dispose();
    }

    [Test]
    public void Dispose_ThenAdd_NoOp()
    {
        var buffer = ArrayBuffer.Rent(16, 4);
        buffer.Dispose();

        // After dispose, Add should be a no-op (not throw)
        var prop = new ParsedProperty(new byte[] { 1 }, 0, 1, "/0");
        buffer.Add(0, prop);
    }

    [Test]
    public void Dispose_ThenGet_ReturnsEmpty()
    {
        var buffer = ArrayBuffer.Rent(16, 4);
        var prop = new ParsedProperty(new byte[] { 1 }, 0, 1, "/0");
        buffer.Add(0, prop);
        buffer.Dispose();

        buffer.Get(0, 0).HasValue.Should().BeFalse();
    }

    [Test]
    public void Rent_Reuses_CachedInstance()
    {
        var buffer1 = ArrayBuffer.Rent(16, 4);
        buffer1.Dispose(); // returns to cache

        var buffer2 = ArrayBuffer.Rent(16, 4);
        buffer2.Should().BeSameAs(buffer1); // reused from cache
        buffer2.Dispose();
    }

    [Test]
    public void DoubleDispose_DoesNotThrow()
    {
        var buffer = ArrayBuffer.Rent(16, 4);
        buffer.Dispose();
        buffer.Dispose(); // should not throw
    }
}
