using QiBalance.Models.DTOs;

namespace QiBalance.Components.ViewModels;

public class DiagnosticViewModel
{
    public bool IsLoading { get; set; }
    public string LoadingMessage { get; set; } = "Ładowanie...";
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }
    
    public DiagnosticQuestion? CurrentQuestion { get; set; }
    public int QuestionNumber { get; set; } = 1;
    public int TotalQuestions { get; set; } = 15;
    public int CurrentPhase => ((QuestionNumber - 1) / 5) + 1;
    public string PhaseDescription => CurrentPhase switch
    {
        1 => "Podstawowa ocena - pytania o stan ogólny i konstytucję",
        2 => "Pogłębiona analiza - szczegółowe objawy i wzorce energetyczne",
        3 => "Specjalistyczna diagnoza - ostateczne ustalenia syndromu TCM",
        _ => "Diagnoza TCM"
    };
    
    public bool IsProcessingAnswer { get; set; }
    public bool? CurrentAnswer { get; set; }
    public bool IsCurrentQuestionAnswered => CurrentAnswer.HasValue;
    
    public string? UserEmail { get; set; }
    public string SessionTimeRemaining { get; set; } = "30:00";
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;

    public void SetCurrentQuestion(DiagnosticQuestion question, int questionNumber)
    {
        CurrentQuestion = question;
        QuestionNumber = questionNumber;
        CurrentAnswer = null;
        LastActivity = DateTime.UtcNow;
    }

    public void SetProcessingAnswer(bool isProcessing, bool? answer = null)
    {
        IsProcessingAnswer = isProcessing;
        if (answer.HasValue)
        {
            CurrentAnswer = answer;
        }
    }

    public void SetError(string errorMessage)
    {
        HasError = true;
        ErrorMessage = errorMessage;
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