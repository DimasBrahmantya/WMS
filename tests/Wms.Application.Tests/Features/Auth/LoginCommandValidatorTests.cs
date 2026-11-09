using FluentAssertions;
using Wms.Application.Features.Auth;
using Xunit;

namespace Wms.Application.Tests.Features.Auth;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidInputs_ShouldNotHaveValidationError()
    {
        // Arrange
        var command = new LoginCommand
        {
            EmailOrUsername = "admin@wms.local",
            Password = "SecurePassword123!"
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData(null, "password")]
    [InlineData("admin", "")]
    [InlineData("admin", null)]
    public void Validate_WithEmptyInputs_ShouldHaveValidationError(string? emailOrUsername, string? password)
    {
        // Arrange
        var command = new LoginCommand
        {
            EmailOrUsername = emailOrUsername!,
            Password = password!
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }
}
