namespace Gluey.Contract.Tests;

[TestFixture]
public class ErrorCollectorTests
{
    [Test]
    public void Created_WithDefaultCapacity_HasCountZero()
    {
        using var collector = new ErrorCollector();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void Created_WithCustomCapacity_HasCountZero()
    {
        using var collector = new ErrorCollector(16);
        collector.Count.Should().Be(0);
    }

    [Test]
    public void Add_IncrementsCount()
    {
        using var collector = new ErrorCollector();
        collector.Add(new ValidationError("/name", ValidationErrorCode.RequiredMissing, "Missing"));

        collector.Count.Should().Be(1);
    }

    [Test]
    public void Add_ErrorIsRetrievableByIndex()
    {
        using var collector = new ErrorCollector();
        var error = new ValidationError("/age", ValidationErrorCode.TypeMismatch, "Wrong type");
        collector.Add(error);

        collector[0].Path.Should().Be("/age");
        collector[0].Code.Should().Be(ValidationErrorCode.TypeMismatch);
        collector[0].Message.Should().Be("Wrong type");
    }

    [Test]
    public void Add_MultipleErrors_AllStoredCorrectly()
    {
        using var collector = new ErrorCollector(8);

        for (int i = 0; i < 7; i++)
        {
            collector.Add(new ValidationError($"/prop{i}", ValidationErrorCode.RequiredMissing, $"Missing {i}"));
        }

        collector.Count.Should().Be(7);

        for (int i = 0; i < 7; i++)
        {
            collector[i].Path.Should().Be($"/prop{i}");
        }
    }

    [Test]
    public void Add_AtCapacity_ReplacesLastSlotWithSentinel()
    {
        using var collector = new ErrorCollector(4);

        // Fill capacity - 1 slots
        for (int i = 0; i < 3; i++)
        {
            collector.Add(new ValidationError($"/prop{i}", ValidationErrorCode.RequiredMissing, $"Missing {i}"));
        }

        // Adding one more should trigger sentinel at last slot
        collector.Add(new ValidationError("/overflow", ValidationErrorCode.TypeMismatch, "Overflow"));

        collector.Count.Should().Be(4);
        collector[3].Code.Should().Be(ValidationErrorCode.TooManyErrors);
        collector[3].Message.Should().ContainEquivalentOf("too many");
    }

    [Test]
    public void Add_BeyondCapacity_SilentlyDropped()
    {
        using var collector = new ErrorCollector(4);

        // Fill all slots (3 normal + 1 sentinel)
        for (int i = 0; i < 4; i++)
        {
            collector.Add(new ValidationError($"/prop{i}", ValidationErrorCode.RequiredMissing, $"Missing {i}"));
        }

        // Add more beyond capacity
        collector.Add(new ValidationError("/extra1", ValidationErrorCode.TypeMismatch, "Extra 1"));
        collector.Add(new ValidationError("/extra2", ValidationErrorCode.TypeMismatch, "Extra 2"));

        collector.Count.Should().Be(4);
    }

    [Test]
    public void HasErrors_WhenEmpty_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        collector.HasErrors.Should().BeFalse();
    }

    [Test]
    public void HasErrors_AfterAdd_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        collector.Add(new ValidationError("/x", ValidationErrorCode.TypeMismatch, "Bad"));

        collector.HasErrors.Should().BeTrue();
    }

    [Test]
    public void Dispose_DoesNotThrow()
    {
        var collector = new ErrorCollector(8);
        var act = () => collector.Dispose();
        act.Should().NotThrow();
    }

    [Test]
    public void Default_HasCountZero()
    {
        var collector = default(ErrorCollector);
        collector.Count.Should().Be(0);
    }

    [Test]
    public void Default_HasErrors_IsFalse()
    {
        var collector = default(ErrorCollector);
        collector.HasErrors.Should().BeFalse();
    }

    [Test]
    public void Default_Dispose_DoesNotThrow()
    {
        var collector = default(ErrorCollector);
        var act = () => collector.Dispose();
        act.Should().NotThrow();
    }

    [Test]
    public void GetEnumerator_AllowsForeach()
    {
        using var collector = new ErrorCollector(8);
        collector.Add(new ValidationError("/a", ValidationErrorCode.RequiredMissing, "Missing a"));
        collector.Add(new ValidationError("/b", ValidationErrorCode.TypeMismatch, "Wrong type b"));

        var paths = new List<string>();
        foreach (var error in collector)
        {
            paths.Add(error.Path);
        }

        paths.Should().BeEquivalentTo(["/a", "/b"]);
    }

    [Test]
    public void GetEnumerator_EmptyCollector_NoIterations()
    {
        using var collector = new ErrorCollector();

        var count = 0;
        foreach (var _ in collector)
        {
            count++;
        }

        count.Should().Be(0);
    }
}
