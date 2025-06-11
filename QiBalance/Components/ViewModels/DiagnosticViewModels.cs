using QiBalance.Models.DTOs;

namespace QiBalance.Components.ViewModels;

/// <summary>
/// ViewModel for diagnostic component with support for both authenticated and anonymous users
/// </summary>
public class DiagnosticViewModel
{
    public DiagnosticQuestion? CurrentQuestion { get; set; }
    public int QuestionNumber { get; set; } = 1;
    public int TotalQuestions { get; set; } = 15;
    public int CurrentPhase { get; set; } = 1;
    public bool IsLoading { get; set; } = false;
    public bool IsProcessingAnswer { get; set; } = false;
    public bool HasError { get; set; } = false;
    public string? ErrorMessage { get; set; }
    public string LoadingMessage { get; set; } = "Ładowanie...";
    
    // User context - can be null for anonymous users
    public string? UserEmail { get; set; }
    
    // Session management - for anonymous users
    public Guid? SessionId { get; set; }
    
    // Session health information
    public string SessionTimeRemaining { get; set; } = "01:00:00";
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    
    // Current answer state
    public bool? CurrentAnswer { get; set; }

    /// <summary>
    /// Indicates if this is an anonymous session
    /// </summary>
    public bool IsAnonymous => string.IsNullOrEmpty(UserEmail);
    
    /// <summary>
    /// Indicates if session is properly initialized
    /// </summary>
    public bool IsSessionInitialized => !IsAnonymous ? !string.IsNullOrEmpty(UserEmail) : SessionId.HasValue;

    /// <summary>
    /// Description of current diagnostic phase
    /// </summary>
    public string PhaseDescription => CurrentPhase switch
    {
        1 => "Podstawowa ocena - pytania o stan ogólny i konstytucję",
        2 => "Pogłębiona analiza - szczegółowe objawy i wzorce energetyczne",
        3 => "Specjalistyczna diagnoza - ostateczne ustalenia syndromu TCM",
        _ => "Diagnoza TCM"
    };

    /// <summary>
    /// Initialize for authenticated user session
    /// </summary>
    public void InitializeForUser(string userEmail)
    {
        UserEmail = userEmail;
        SessionId = null;
        ClearError();
    }

    /// <summary>
    /// Initialize for anonymous session
    /// </summary>
    public void InitializeForAnonymous(Guid sessionId)
    {
        UserEmail = null;
        SessionId = sessionId;
        ClearError();
    }

    /// <summary>
    /// Set current question and update progress
    /// </summary>
    public void SetCurrentQuestion(DiagnosticQuestion question, int questionNumber)
    {
        CurrentQuestion = question;
        QuestionNumber = questionNumber;
        CurrentPhase = GetPhaseFromQuestionNumber(questionNumber);
        CurrentAnswer = null; // Reset current answer
        ClearError();
    }

    /// <summary>
    /// Set processing state for current answer
    /// </summary>
    public void SetProcessingAnswer(bool isProcessing, bool? currentAnswer = null)
    {
        IsProcessingAnswer = isProcessing;
        if (currentAnswer.HasValue)
        {
            CurrentAnswer = currentAnswer;
        }
    }

    /// <summary>
    /// Set error state
    /// </summary>
    public void SetError(string errorMessage)
    {
        HasError = true;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Clear error state
    /// </summary>
    public void ClearError()
    {
        HasError = false;
        ErrorMessage = null;
    }

    /// <summary>
    /// Update session health information
    /// </summary>
    public void UpdateSessionInfo(string timeRemaining)
    {
        SessionTimeRemaining = timeRemaining;
        LastActivity = DateTime.UtcNow;
    }

    /// <summary>
    /// Get phase number from question number (1-5: Phase 1, 6-10: Phase 2, 11-15: Phase 3)
    /// </summary>
    private static int GetPhaseFromQuestionNumber(int questionNumber)
    {
        return questionNumber switch
        {
            <= 5 => 1,
            <= 10 => 2,
            <= 15 => 3,
            _ => 3
        };
    }
}

/// <summary>
/// ViewModel for history page managing user recommendations with pagination
/// </summary>
public class HistoryViewModel
{
    public List<RecommendationEntity> Recommendations { get; set; } = new();
    public bool IsLoading { get; set; } = false;
    public bool HasError { get; set; } = false;
    public string? ErrorMessage { get; set; }
    
    // Pagination
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; } = 0;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    
    // Selected recommendation for modal
    public RecommendationEntity? SelectedRecommendation { get; set; }

    /// <summary>
    /// Indicates if there are any recommendations to display
    /// </summary>
    public bool HasRecommendations => Recommendations?.Any() == true;

    /// <summary>
    /// Set recommendations from paged result
    /// </summary>
    public void SetRecommendations(PagedResult<RecommendationEntity> pagedResult)
    {
        Recommendations = pagedResult.Items.ToList();
        CurrentPage = pagedResult.Page;
        TotalCount = pagedResult.TotalCount;
        ClearError();
    }

    /// <summary>
    /// Set selected recommendation for modal display
    /// </summary>
    public void SetSelectedRecommendation(RecommendationEntity recommendation)
    {
        SelectedRecommendation = recommendation;
    }

    /// <summary>
    /// Clear selected recommendation (close modal)
    /// </summary>
    public void ClearSelectedRecommendation()
    {
        SelectedRecommendation = null;
    }

    /// <summary>
    /// Set error state
    /// </summary>
    public void SetError(string errorMessage)
    {
        HasError = true;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Clear error state
    /// </summary>
    public void ClearError()
    {
        HasError = false;
        ErrorMessage = null;
    }

    /// <summary>
    /// Check if current page has previous pages
    /// </summary>
    public bool HasPreviousPage => CurrentPage > 1;

    /// <summary>
    /// Check if current page has next pages
    /// </summary>
    public bool HasNextPage => CurrentPage < TotalPages;

    /// <summary>
    /// Get display text for pagination info
    /// </summary>
    public string GetPaginationInfo()
    {
        if (TotalCount == 0)
            return "Brak rekordów";

        var startItem = (CurrentPage - 1) * PageSize + 1;
        var endItem = Math.Min(CurrentPage * PageSize, TotalCount);
        
        return $"Wyświetlane {startItem}-{endItem} z {TotalCount}";
    }
}

// Additional diagnostic progress model for auto-save
public class DiagnosticProgress
{
    public int QuestionNumber { get; set; }
    public int CurrentPhase { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public DateTime LastActivity { get; set; }
    public Dictionary<int, bool> Answers { get; set; } = new();
}

// Performance monitoring model
public class DiagnosticPerformanceMetrics
{
    public TimeSpan PageLoadTime { get; set; }
    public TimeSpan AverageQuestionResponseTime { get; set; }
    public int TotalQuestions { get; set; }
    public int ErrorCount { get; set; }
    public DateTime SessionStartTime { get; set; }
    public DateTime SessionEndTime { get; set; }
    public List<PhaseTransitionMetric> PhaseTransitions { get; set; } = new();
}

public class PhaseTransitionMetric
{
    public int FromPhase { get; set; }
    public int ToPhase { get; set; }
    public TimeSpan TransitionTime { get; set; }
    public DateTime Timestamp { get; set; }
}

// Enhanced error tracking model
public class DiagnosticErrorInfo
{
    public string ErrorType { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public int QuestionNumber { get; set; }
    public int Phase { get; set; }
    public DateTime Timestamp { get; set; }
    public string UserAction { get; set; } = string.Empty;
    public bool IsRetryable { get; set; }
    public int RetryCount { get; set; }
} 