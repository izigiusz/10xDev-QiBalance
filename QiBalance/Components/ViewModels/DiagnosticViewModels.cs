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

    public void SetCurrentQuestion(DiagnosticQuestion question, int questionNumber)
    {
        CurrentQuestion = question;
        QuestionNumber = questionNumber;
        CurrentPhase = GetPhaseFromQuestionNumber(questionNumber);
        CurrentAnswer = null;
        LastActivity = DateTime.UtcNow;
    }

    public void SetProcessingAnswer(bool isProcessing, bool? answer = null)
    {
        IsProcessingAnswer = isProcessing;
        CurrentAnswer = answer;
        if (isProcessing)
            LastActivity = DateTime.UtcNow;
    }

    public void SetError(string message)
    {
        HasError = true;
        ErrorMessage = message;
        IsLoading = false;
        IsProcessingAnswer = false;
    }

    public void ClearError()
    {
        HasError = false;
        ErrorMessage = null;
    }

    public void UpdateSessionInfo(string timeRemaining)
    {
        SessionTimeRemaining = timeRemaining;
        LastActivity = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Initialize for authenticated user
    /// </summary>
    public void InitializeForUser(string userEmail)
    {
        UserEmail = userEmail;
        SessionId = null; // Clear session ID for authenticated users
    }
    
    /// <summary>
    /// Initialize for anonymous user
    /// </summary> 
    public void InitializeForAnonymous(Guid sessionId)
    {
        SessionId = sessionId;
        UserEmail = null; // Clear user email for anonymous users
    }

    private int GetPhaseFromQuestionNumber(int questionNumber) => ((questionNumber - 1) / 5) + 1;
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