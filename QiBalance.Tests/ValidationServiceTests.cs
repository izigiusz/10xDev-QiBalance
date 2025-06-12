using System;
using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using QiBalance.Services;
using Xunit;

namespace QiBalance.Tests
{
    /// <summary>
    /// Unit tests for ValidationService focusing on ValidateUserId method
    /// Tests cover all critical scenarios for user ID validation
    /// </summary>
    public class ValidationServiceTests
    {
        private readonly Mock<ILogger<ValidationService>> _loggerMock;
        private readonly ValidationService _validationService;

        public ValidationServiceTests()
        {
            _loggerMock = new Mock<ILogger<ValidationService>>();
            _validationService = new ValidationService(_loggerMock.Object);
        }

        #region ValidateUserId Tests

        [Theory]
        [InlineData("550e8400-e29b-41d4-a716-446655440000")] // Valid GUID
        [InlineData("12345678-1234-5678-9012-123456789012")] // Another valid GUID
        [InlineData("a1b2c3d4-e5f6-7890-abcd-ef1234567890")] // Mixed case GUID
        public void ValidateUserId_WithValidGuid_ShouldNotThrowException(string validGuid)
        {
            // Act & Assert
            Action act = () => _validationService.ValidateUserId(validGuid);
            act.Should().NotThrow();
        }

        [Theory]
        [InlineData("test@example.com")]
        [InlineData("user.name@domain.co.uk")]
        [InlineData("test123+tag@gmail.com")]
        [InlineData("firstname.lastname@company.org")]
        [InlineData("a@b.co")]
        public void ValidateUserId_WithValidEmail_ShouldNotThrowException(string validEmail)
        {
            // Act & Assert
            Action act = () => _validationService.ValidateUserId(validEmail);
            act.Should().NotThrow();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        [InlineData("\n")]
        public void ValidateUserId_WithNullOrWhitespace_ShouldThrowValidationException(string? invalidUserId)
        {
            // Act & Assert
            Action act = () => _validationService.ValidateUserId(invalidUserId);
            act.Should().Throw<ValidationException>()
                .WithMessage("Identyfikator użytkownika jest wymagany");
        }

        [Theory]
        [InlineData("not-an-email")]
        [InlineData("invalid@")]
        [InlineData("@invalid.com")]
        [InlineData("invalid@.com")]
        [InlineData("invalid.email")]
        [InlineData("invalid@test")]
        [InlineData("123456")]
        [InlineData("not-a-guid-either")]
        [InlineData("550e8400-e29b-41d4-a716")] // Incomplete GUID
        [InlineData("550e8400-e29b-41d4-a716-446655440000-extra")] // Too long GUID
        public void ValidateUserId_WithInvalidFormat_ShouldThrowValidationException(string invalidUserId)
        {
            // Act & Assert
            Action act = () => _validationService.ValidateUserId(invalidUserId);
            act.Should().Throw<ValidationException>()
                .WithMessage("Identyfikator użytkownika musi być prawidłowym adresem email lub identyfikatorem GUID");
        }

        [Fact]
        public void ValidateUserId_WithValidInput_ShouldLogNoWarnings()
        {
            // Arrange
            var validGuid = Guid.NewGuid().ToString();

            // Act
            _validationService.ValidateUserId(validGuid);

            // Assert - No warning logs should be called for valid input
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Fact]
        public void ValidateUserId_WithNullInput_ShouldLogWarning()
        {
            // Act & Assert
            Action act = () => _validationService.ValidateUserId(null);
            act.Should().Throw<ValidationException>();

            // Verify warning was logged
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("UserId validation failed: null or empty")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void ValidateUserId_WithInvalidFormat_ShouldLogWarning()
        {
            // Arrange
            var invalidUserId = "invalid-format";

            // Act & Assert
            Action act = () => _validationService.ValidateUserId(invalidUserId);
            act.Should().Throw<ValidationException>();

            // Verify warning was logged
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("UserId validation failed: invalid format")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Edge Cases and Security Tests

        [Theory]
        [InlineData("00000000-0000-0000-0000-000000000000")] // Empty GUID (valid GUID format)
        [InlineData("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")] // Max GUID (valid GUID format)
        public void ValidateUserId_WithEdgeCaseGuids_ShouldNotThrowException(string edgeCaseGuid)
        {
            // Act & Assert
            Action act = () => _validationService.ValidateUserId(edgeCaseGuid);
            act.Should().NotThrow();
        }

        [Theory]
        [InlineData("test@sub.domain.example.com")] // Multiple subdomains
        [InlineData("very.long.email.address.with.many.dots@very.long.domain.name.example.org")] // Long email
        public void ValidateUserId_WithComplexValidEmails_ShouldNotThrowException(string complexEmail)
        {
            // Act & Assert
            Action act = () => _validationService.ValidateUserId(complexEmail);
            act.Should().NotThrow();
        }

        [Theory]
        [InlineData("<script>alert('xss')</script>@test.com")] // XSS attempt
        [InlineData("../../../etc/passwd")] // Path traversal attempt
        [InlineData("'; DROP TABLE users; --")] // SQL injection attempt
        public void ValidateUserId_WithMaliciousInput_ShouldThrowValidationException(string maliciousInput)
        {
            // Act & Assert
            Action act = () => _validationService.ValidateUserId(maliciousInput);
            act.Should().Throw<ValidationException>();
        }

        #endregion

        #region Performance Tests

        [Fact]
        public void ValidateUserId_MultipleValidations_ShouldPerformConsistently()
        {
            // Arrange
            var validInputs = new[]
            {
                Guid.NewGuid().ToString(),
                "test1@example.com",
                Guid.NewGuid().ToString(),
                "test2@example.com",
                Guid.NewGuid().ToString()
            };

            // Act & Assert - All should pass without throwing
            foreach (var input in validInputs)
            {
                Action act = () => _validationService.ValidateUserId(input);
                act.Should().NotThrow();
            }
        }

        #endregion
    }
} 