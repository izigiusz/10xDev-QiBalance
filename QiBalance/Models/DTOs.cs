using System.ComponentModel.DataAnnotations;

namespace QiBalance.Models.DTOs
{
    /// <summary>
    /// Enum for question types in diagnostic flow
    /// </summary>
    public enum QuestionType
    {
        YesNo = 0
    }

    /// <summary>
    /// Enum for sorting recommendations
    /// </summary>
    public enum SortOrder
    {
        DateDesc = 0,
        DateAsc = 1
    }

    /// <summary>
    /// DTO representing a single diagnostic question
    /// </summary>
    public class DiagnosticQuestion
    {
        public string Id { get; set; } = string.Empty;
        public string QuestionText { get; set; } = string.Empty;
        public QuestionType QuestionType { get; set; } = QuestionType.YesNo;
    }

    /// <summary>
    /// DTO representing user's answer to a diagnostic question
    /// </summary>
    public class DiagnosticAnswer
    {
        public string QuestionId { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public bool Answer { get; set; }
        public DateTime AnsweredAt { get; set; }
    }

    /// <summary>
    /// DTO representing an active diagnostic session with 3-phase approach
    /// </summary>
    public class DiagnosticSession
    {
        public Guid SessionId { get; set; }
        public List<DiagnosticQuestion> Questions { get; set; } = new();
        public int TotalQuestions { get; set; } = 15;
        public int CurrentQuestion { get; set; } = 1;
        public int CurrentPhase { get; set; } = 1; // Nowe: 1, 2, 3
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string? UserId { get; set; }
        public List<DiagnosticAnswer> Answers { get; set; } = new();
        public string? InitialSymptoms { get; set; } // Zachowujemy dla kolejnych faz
    }

    /// <summary>
    /// DTO for diagnostic flow responses with phase information
    /// </summary>
    public class DiagnosticResponse
    {
        public bool HasMoreQuestions { get; set; }
        public DiagnosticQuestion? NextQuestion { get; set; }
        public int CurrentQuestion { get; set; }
        public int TotalQuestions { get; set; }
        public int CurrentPhase { get; set; } // Nowe
        public RecommendationResult? Recommendations { get; set; }
    }

    /// <summary>
    /// DTO representing a diagnostic phase with questions
    /// </summary>
    public class DiagnosticPhase
    {
        public int Phase { get; set; }
        public string PhaseName { get; set; } = string.Empty;
        public string PhaseDescription { get; set; } = string.Empty;
        public List<DiagnosticQuestion> Questions { get; set; } = new();
    }

    /// <summary>
    /// DTO containing AI-generated recommendation content
    /// </summary>
    public class RecommendationResult
    {
        public string RecommendationText { get; set; } = string.Empty;
        public string TcmSyndrome { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for recommendation entity used in UI components
    /// Maps to Database.Recommendation but with UI-specific formatting
    /// </summary>
    public class RecommendationEntity
    {
        public Guid RecommendationId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public DateTime DateGenerated { get; set; }
        public string RecommendationText { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for paginated results
    /// </summary>
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int Limit { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / Limit);
        public bool HasNextPage => Page < TotalPages;
        public bool HasPreviousPage => Page > 1;
    }

    /// <summary>
    /// DTO for authentication results
    /// </summary>
    public class AuthResult
    {
        public bool Success { get; set; }
        public string? UserEmail { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// DTO for user context management
    /// </summary>
    public class UserContext
    {
        public string? Email { get; set; }
        public bool IsAuthenticated => !string.IsNullOrEmpty(Email);
    }

    /// <summary>
    /// DTO for starting diagnostic session
    /// </summary>
    public class StartDiagnosticRequest
    {
        public string? InitialSymptoms { get; set; }
        public string? UserId { get; set; }
    }

    /// <summary>
    /// DTO for submitting diagnostic answers
    /// </summary>
    public class SubmitAnswerRequest
    {
        [Required]
        public Guid SessionId { get; set; }
        
        [Required]
        public string QuestionId { get; set; } = string.Empty;
        
        [Required]
        public bool Answer { get; set; }
        
        public string? UserId { get; set; }
    }

    /// <summary>
    /// DTO for saving recommendations
    /// </summary>
    public class SaveRecommendationRequest
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        [Required]
        public RecommendationResult RecommendationResult { get; set; } = new();
    }
} 