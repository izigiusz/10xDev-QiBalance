using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace QiBalance.Services
{
    /// <summary>
    /// Interface for centralized validation logic across the application
    /// </summary>
    public interface IValidationService
    {
        void ValidateSymptoms(string? symptoms);
        void ValidateUserId(string? userId);
        void ValidateRecommendationText(string? text);
        void ValidatePagination(int page, int limit);
        void ValidateSessionId(Guid sessionId);
        void ValidateQuestionId(string? questionId);
    }

    /// <summary>
    /// Service providing centralized validation with consistent error messages
    /// All validation methods throw ValidationException for invalid input
    /// </summary>
    public class ValidationService : IValidationService
    {
        private readonly ILogger<ValidationService> _logger;
        
        // Validation constants
        private const int MaxSymptomsLength = 1000;
        private const int MaxRecommendationLength = 10000;
        private const int MinPageNumber = 1;
        private const int MinLimitNumber = 1;
        private const int MaxLimitNumber = 50;
        
        // Email validation regex
        private static readonly Regex EmailRegex = new(
            @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public ValidationService(ILogger<ValidationService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Validates initial symptoms text for diagnostic session
        /// </summary>
        public void ValidateSymptoms(string? symptoms)
        {
            if (string.IsNullOrEmpty(symptoms))
            {
                // Symptoms are optional - empty/null is valid
                return;
            }

            if (symptoms.Length > MaxSymptomsLength)
            {
                _logger.LogWarning("Symptoms validation failed: text too long ({Length} > {MaxLength})", 
                    symptoms.Length, MaxSymptomsLength);
                throw new ValidationException($"Opis objawów nie może przekraczać {MaxSymptomsLength} znaków");
            }

            // Sanitize symptoms text
            var trimmedSymptoms = symptoms.Trim();
            if (trimmedSymptoms != symptoms)
            {
                _logger.LogDebug("Symptoms text was trimmed during validation");
            }
        }

        /// <summary>
        /// Validates user ID which should be a valid email address
        /// </summary>
        public void ValidateUserId(string? userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("UserId validation failed: null or empty");
                throw new ValidationException("Identyfikator użytkownika jest wymagany");
            }

            if (!IsValidEmail(userId))
            {
                _logger.LogWarning("UserId validation failed: invalid email format for {UserId}", userId);
                throw new ValidationException("Identyfikator użytkownika musi być prawidłowym adresem email");
            }
        }

        /// <summary>
        /// Validates recommendation text content
        /// </summary>
        public void ValidateRecommendationText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Recommendation text validation failed: null or empty");
                throw new ValidationException("Tekst rekomendacji jest wymagany");
            }

            if (text.Length > MaxRecommendationLength)
            {
                _logger.LogWarning("Recommendation text validation failed: text too long ({Length} > {MaxLength})", 
                    text.Length, MaxRecommendationLength);
                throw new ValidationException($"Tekst rekomendacji nie może przekraczać {MaxRecommendationLength} znaków");
            }
        }

        /// <summary>
        /// Validates pagination parameters for list requests
        /// </summary>
        public void ValidatePagination(int page, int limit)
        {
            if (page < MinPageNumber)
            {
                _logger.LogWarning("Pagination validation failed: invalid page number {Page}", page);
                throw new ValidationException($"Numer strony musi być większy lub równy {MinPageNumber}");
            }

            if (limit < MinLimitNumber || limit > MaxLimitNumber)
            {
                _logger.LogWarning("Pagination validation failed: invalid limit {Limit}", limit);
                throw new ValidationException($"Limit wyników musi być między {MinLimitNumber} a {MaxLimitNumber}");
            }
        }

        /// <summary>
        /// Validates diagnostic session ID
        /// </summary>
        public void ValidateSessionId(Guid sessionId)
        {
            if (sessionId == Guid.Empty)
            {
                _logger.LogWarning("SessionId validation failed: empty GUID");
                throw new ValidationException("Identyfikator sesji diagnostycznej jest nieprawidłowy");
            }
        }

        /// <summary>
        /// Validates diagnostic question ID
        /// </summary>
        public void ValidateQuestionId(string? questionId)
        {
            if (string.IsNullOrWhiteSpace(questionId))
            {
                _logger.LogWarning("QuestionId validation failed: null or empty");
                throw new ValidationException("Identyfikator pytania jest wymagany");
            }
        }

        /// <summary>
        /// Helper method to validate email format
        /// </summary>
        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                return EmailRegex.IsMatch(email);
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }
    }
} 