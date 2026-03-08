using System.Reflection;

namespace Gluey.Contract.Tests;

[TestFixture]
public class ValidationErrorTests
{
    [Test]
    public void ValidationErrorCode_IsBackedByByte()
    {
        Enum.GetUnderlyingType(typeof(ValidationErrorCode)).Should().Be(typeof(byte));
    }

    [Test]
    public void ValidationErrorCode_HasNoneAsZero()
    {
        ((byte)ValidationErrorCode.None).Should().Be(0);
    }

    [Test]
    public void ValidationErrorCode_HasTypeMismatch()
    {
        Enum.IsDefined(typeof(ValidationErrorCode), ValidationErrorCode.TypeMismatch).Should().BeTrue();
    }

    [Test]
    public void ValidationErrorCode_HasRequiredMissing()
    {
        Enum.IsDefined(typeof(ValidationErrorCode), ValidationErrorCode.RequiredMissing).Should().BeTrue();
    }

    [Test]
    public void ValidationErrorCode_HasTooManyErrors()
    {
        Enum.IsDefined(typeof(ValidationErrorCode), ValidationErrorCode.TooManyErrors).Should().BeTrue();
    }

    [Test]
    public void ValidationErrorCode_HasAtLeast28Values()
    {
        var values = Enum.GetValues<ValidationErrorCode>();
        values.Length.Should().BeGreaterThanOrEqualTo(28);
    }

    [Test]
    public void ValidationError_IsReadonlyStruct()
    {
        var type = typeof(ValidationError);
        type.IsValueType.Should().BeTrue();
        type.GetCustomAttribute<System.Runtime.CompilerServices.IsReadOnlyAttribute>()
            .Should().NotBeNull("ValidationError should be a readonly struct");
    }

    [Test]
    public void ValidationError_Constructor_ExposesAllFields()
    {
        var error = new ValidationError("/foo/bar", ValidationErrorCode.TypeMismatch, "Type mismatch");

        error.Path.Should().Be("/foo/bar");
        error.Code.Should().Be(ValidationErrorCode.TypeMismatch);
        error.Message.Should().Be("Type mismatch");
    }

    [Test]
    public void ValidationErrorMessages_Get_TypeMismatch_ReturnsNonEmptyString()
    {
        var message = ValidationErrorMessages.Get(ValidationErrorCode.TypeMismatch);
        message.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void ValidationErrorMessages_Get_TooManyErrors_ReturnsSentinelMessage()
    {
        var message = ValidationErrorMessages.Get(ValidationErrorCode.TooManyErrors);
        message.Should().NotBeNullOrEmpty();
        message.Should().ContainEquivalentOf("too many", Exactly.Once(), "sentinel message should indicate truncation");
    }

    [Test]
    public void ValidationErrorMessages_Get_None_ReturnsEmptyString()
    {
        var message = ValidationErrorMessages.Get(ValidationErrorCode.None);
        message.Should().BeEmpty();
    }

    [Test]
    public void ValidationErrorMessages_EveryCodeExceptNone_HasNonEmptyMessage()
    {
        var codes = Enum.GetValues<ValidationErrorCode>();

        foreach (var code in codes)
        {
            if (code == ValidationErrorCode.None)
                continue;

            var message = ValidationErrorMessages.Get(code);
            message.Should().NotBeNullOrEmpty($"ValidationErrorCode.{code} should have a message");
        }
    }
}
