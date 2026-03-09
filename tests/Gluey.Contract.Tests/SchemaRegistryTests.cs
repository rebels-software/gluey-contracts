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
