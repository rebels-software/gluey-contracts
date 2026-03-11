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

namespace Gluey.Contract.Tests;

[TestFixture]
[Category("SchemaRef")]
internal sealed class SchemaRegistryTests
{
    private SchemaRegistry _registry = null!;

    [SetUp]
    public void SetUp()
    {
        _registry = new SchemaRegistry();
    }

    [Test]
    public void Add_ThenTryGet_ReturnsTrueAndSameNode()
    {
        var node = new SchemaNode("");
        _registry.Add("https://example.com/schema", node);

        _registry.TryGet("https://example.com/schema", out var result).Should().BeTrue();
        result.Should().BeSameAs(node);
    }

    [Test]
    public void TryGet_UnregisteredUri_ReturnsFalseAndNull()
    {
        _registry.TryGet("https://example.com/missing", out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Test]
    public void Add_DuplicateUri_OverwritesPrevious()
    {
        var first = new SchemaNode("");
        var second = new SchemaNode("");
        _registry.Add("https://example.com/schema", first);
        _registry.Add("https://example.com/schema", second);

        _registry.TryGet("https://example.com/schema", out var result).Should().BeTrue();
        result.Should().BeSameAs(second);
    }

    [Test]
    public void Add_NullUri_ThrowsArgumentNullException()
    {
        var node = new SchemaNode("");
        var act = () => _registry.Add(null!, node);
        act.Should().Throw<ArgumentNullException>().WithParameterName("uri");
    }

    [Test]
    public void Add_NullRoot_ThrowsArgumentNullException()
    {
        var act = () => _registry.Add("https://example.com/schema", null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("root");
    }

    [Test]
    public void Count_AfterAdding_ReflectsNumberOfEntries()
    {
        _registry.Count.Should().Be(0);

        _registry.Add("https://example.com/a", new SchemaNode(""));
        _registry.Count.Should().Be(1);

        _registry.Add("https://example.com/b", new SchemaNode(""));
        _registry.Count.Should().Be(2);
    }

    [Test]
    public void Add_UriWithTrailingSlash_NormalizedOnRetrieval()
    {
        var node = new SchemaNode("");
        _registry.Add("https://example.com/schema/", node);

        _registry.TryGet("https://example.com/schema", out var result).Should().BeTrue();
        result.Should().BeSameAs(node);
    }
}
