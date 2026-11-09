using FluentAssertions;
using Wms.Application.Common.Models;
using Xunit;

namespace Wms.Application.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        // Act
        var result = Result.Success();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Failure_WithSingleError_ShouldContainError()
    {
        // Act
        var result = Result.Failure("Terjadi kesalahan sistem.");

        // Assert
        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Be("Terjadi kesalahan sistem.");
        result.Errors.Should().ContainSingle().Which.Should().Be("Terjadi kesalahan sistem.");
    }

    [Fact]
    public void TypedResult_Success_ShouldContainData()
    {
        // Act
        var result = Result<int>.Success(42);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().Be(42);
        result.ErrorMessage.Should().BeNull();
    }
}
