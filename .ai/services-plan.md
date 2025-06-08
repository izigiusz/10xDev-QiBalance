# QiBalance Services Architecture Plan

## 1. Service Overview

### Core Services
- **IDiagnosticService** - manages diagnostic flow and AI integration
- **IRecommendationService** - handles recommendation CRUD operations
- **IAuthService** - manages user authentication and context
- **ISupabaseService** - abstracts Supabase database operations

### Supporting Services
- **IOpenAIService** - wrapper for Semantic Kernel integration
- **IValidationService** - centralized validation logic

## 2. Service Contracts

### IDiagnosticService
```csharp
public interface IDiagnosticService
{
    Task<DiagnosticSession> StartSessionAsync(string? initialSymptoms, string? userId = null);
    Task<DiagnosticResponse> SubmitAnswerAsync(Guid sessionId, string questionId, bool answer, string? userId = null);
    Task<bool> IsSessionValidAsync(Guid sessionId);
    Task ClearExpiredSessionsAsync();
}
```

### IRecommendationService
```csharp
public interface IRecommendationService
{
    Task<RecommendationEntity> SaveRecommendationAsync(string userId, RecommendationResult recommendationResult);
    Task<PagedResult<RecommendationEntity>> GetUserRecommendationsAsync(
        string userId, 
        int page = 1, 
        int limit = 10, 
        SortOrder sort = SortOrder.DateDesc);
    Task<RecommendationEntity?> GetRecommendationByIdAsync(Guid recommendationId, string userId);
    Task<bool> DeleteRecommendationAsync(Guid recommendationId, string userId);
}
```

### IAuthService
```csharp
public interface IAuthService
{
    Task<AuthResult> ValidateSessionAsync(string accessToken);
    Task<string?> GetCurrentUserEmailAsync();
    Task SetUserContextAsync(string userEmail);
    Task ClearUserContextAsync();
    bool IsUserAuthenticated { get; }
    string? CurrentUserEmail { get; }
}
```

### IOpenAIService (Podejście Hybrydowe 3-Fazowe)
```csharp
public interface IOpenAIService
{
    Task<DiagnosticPhase> GeneratePhaseQuestionsAsync(
        int phase, 
        string? initialSymptoms, 
        List<DiagnosticAnswer> previousAnswers);
    Task<RecommendationResult> GenerateRecommendationsAsync(
        string? initialSymptoms, 
        List<DiagnosticAnswer> diagnosticAnswers);
}
```

### ISupabaseService
```csharp
public interface ISupabaseService
{
    Task<T?> GetByIdAsync<T>(object id) where T : class;
    Task<List<T>> GetByUserIdAsync<T>(string userId) where T : class;
    Task<T> InsertAsync<T>(T entity) where T : class;
    Task<T> UpdateAsync<T>(T entity) where T : class;
    Task<bool> DeleteAsync<T>(object id, string userId) where T : class;
    Task<PagedResult<T>> GetPagedAsync<T>(string userId, int page, int limit, string sortBy) where T : class;
}
```

## 3. Data Transfer Objects

### DiagnosticSession (Podejście 3-Fazowe)
```csharp
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
```

### DiagnosticQuestion
```csharp
public class DiagnosticQuestion
{
    public string Id { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public QuestionType QuestionType { get; set; } = QuestionType.YesNo;
}
```

### DiagnosticAnswer
```csharp
public class DiagnosticAnswer
{
    public string QuestionId { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public bool Answer { get; set; }
    public DateTime AnsweredAt { get; set; }
}
```

### DiagnosticResponse (Z Informacją o Fazie)
```csharp
public class DiagnosticResponse
{
    public bool HasMoreQuestions { get; set; }
    public DiagnosticQuestion? NextQuestion { get; set; }
    public int CurrentQuestion { get; set; }
    public int TotalQuestions { get; set; }
    public int CurrentPhase { get; set; } // Nowe
    public RecommendationResult? Recommendations { get; set; }
}
```

### DiagnosticPhase (Nowy DTO)
```csharp
public class DiagnosticPhase
{
    public int Phase { get; set; }
    public string PhaseName { get; set; } = string.Empty;
    public string PhaseDescription { get; set; } = string.Empty;
    public List<DiagnosticQuestion> Questions { get; set; } = new();
}
```

### RecommendationResult
```csharp
public class RecommendationResult
{
    public string RecommendationText { get; set; } = string.Empty;
    public string TcmSyndrome { get; set; } = string.Empty;
}
```

### RecommendationEntity
```csharp
public class RecommendationEntity
{
    public Guid RecommendationId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime DateGenerated { get; set; }
    public string RecommendationText { get; set; } = string.Empty;
}
```

## 4. Service Implementations

### DiagnosticService Implementation (Podejście 3-Fazowe)
```csharp
public class DiagnosticService : IDiagnosticService
{
    private readonly IOpenAIService _openAIService;
    private readonly IMemoryCache _cache;
    private readonly IValidationService _validationService;
    
    public async Task<DiagnosticSession> StartSessionAsync(string? initialSymptoms, string? userId = null)
    {
        // Walidacja
        _validationService.ValidateSymptoms(initialSymptoms);
        
        // Generuj pierwszą fazę pytań (5 pytań bazowych)
        var phase1 = await _openAIService.GeneratePhaseQuestionsAsync(1, initialSymptoms, new List<DiagnosticAnswer>());
        
        // Utwórz sesję
        var session = new DiagnosticSession
        {
            SessionId = Guid.NewGuid(),
            Questions = phase1.Questions,
            CurrentPhase = 1,
            InitialSymptoms = initialSymptoms,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            UserId = userId
        };
        
        // Cachuj sesję
        _cache.Set($"diagnostic_session_{session.SessionId}", session, TimeSpan.FromHours(1));
        
        return session;
    }
    
    public async Task<DiagnosticResponse> SubmitAnswerAsync(Guid sessionId, string questionId, bool answer, string? userId = null)
    {
        // Pobierz sesję z cache
        var session = _cache.Get<DiagnosticSession>($"diagnostic_session_{sessionId}");
        if (session == null || session.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("Session expired or not found");
            
        // Dodaj odpowiedź do sesji
        var diagnosticAnswer = new DiagnosticAnswer
        {
            QuestionId = questionId,
            Question = session.Questions.First(q => q.Id == questionId).QuestionText,
            Answer = answer,
            AnsweredAt = DateTime.UtcNow
        };
        session.Answers.Add(diagnosticAnswer);
        session.CurrentQuestion++;
        
        // Sprawdź czy kończymy fazę (po 5 i 10 pytaniach)
        if ((session.CurrentQuestion == 6 || session.CurrentQuestion == 11) && session.CurrentQuestion <= session.TotalQuestions)
        {
            // Generuj następną fazę (2 lub 3)
            session.CurrentPhase = session.CurrentQuestion == 6 ? 2 : 3;
            var nextPhase = await _openAIService.GeneratePhaseQuestionsAsync(
                session.CurrentPhase, 
                session.InitialSymptoms, 
                session.Answers);
            
            // Dodaj nowe pytania do sesji
            session.Questions.AddRange(nextPhase.Questions);
        }
        
        // Sprawdź czy mamy więcej pytań
        if (session.CurrentQuestion <= session.TotalQuestions)
        {
            var nextQuestion = session.Questions
                .Skip(session.CurrentQuestion - 1)
                .FirstOrDefault();
            
            // Aktualizuj cache
            _cache.Set($"diagnostic_session_{sessionId}", session, TimeSpan.FromHours(1));
            
            return new DiagnosticResponse
            {
                HasMoreQuestions = true,
                NextQuestion = nextQuestion,
                CurrentQuestion = session.CurrentQuestion,
                TotalQuestions = session.TotalQuestions,
                CurrentPhase = session.CurrentPhase
            };
        }
        else
        {
            // Generuj finalne rekomendacje
            var recommendations = await _openAIService.GenerateRecommendationsAsync(
                session.InitialSymptoms, 
                session.Answers);
            
            // Usuń sesję z cache
            _cache.Remove($"diagnostic_session_{sessionId}");
            
            return new DiagnosticResponse
            {
                HasMoreQuestions = false,
                Recommendations = recommendations
            };
        }
    }
}
```

### RecommendationService Implementation
```csharp
public class RecommendationService : IRecommendationService
{
    private readonly ISupabaseService _supabaseService;
    private readonly IValidationService _validationService;
    
    public async Task<RecommendationEntity> SaveRecommendationAsync(string userId, RecommendationResult recommendationResult)
    {
        // Validation
        _validationService.ValidateUserId(userId);
        _validationService.ValidateRecommendationText(recommendationResult.RecommendationText);
        
        var recommendation = new RecommendationEntity
        {
            RecommendationId = Guid.NewGuid(),
            UserId = userId,
            DateGenerated = DateTime.UtcNow,
            RecommendationText = recommendationResult.RecommendationText
        };
        
        return await _supabaseService.InsertAsync(recommendation);
    }
    
    public async Task<PagedResult<RecommendationEntity>> GetUserRecommendationsAsync(
        string userId, int page = 1, int limit = 10, SortOrder sort = SortOrder.DateDesc)
    {
        _validationService.ValidateUserId(userId);
        _validationService.ValidatePagination(page, limit);
        
        var sortBy = sort == SortOrder.DateDesc ? "date_generated desc" : "date_generated asc";
        return await _supabaseService.GetPagedAsync<RecommendationEntity>(userId, page, limit, sortBy);
    }
}
```

## 5. Dependency Injection Configuration

### Program.cs Setup
```csharp
// Core Services
builder.Services.AddScoped<IDiagnosticService, DiagnosticService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOpenAIService, OpenAIService>();
builder.Services.AddScoped<ISupabaseService, SupabaseService>();
builder.Services.AddScoped<IValidationService, ValidationService>();

// External Services
builder.Services.AddScoped<Supabase.Client>(provider => 
    new Supabase.Client(supabaseUrl, supabaseKey));

// Semantic Kernel for OpenAI
builder.Services.AddKernel()
    .AddOpenAIChatCompletion("gpt-4", openAIKey);

// Caching
builder.Services.AddMemoryCache();

// Session State (for user context)
builder.Services.AddScoped<UserContext>();
```

## 6. Component Usage Examples

### Diagnostic Component (Podejście 3-Fazowe)
```csharp
@page "/diagnostic"
@inject IDiagnosticService DiagnosticService
@inject IAuthService AuthService

<div class="diagnostic-container">
    <div class="progress-container">
        <div class="progress-bar">
            <div class="progress-fill" style="width: @(((float)currentQuestionNumber / totalQuestions) * 100)%"></div>
        </div>
        <p>Pytanie @currentQuestionNumber z @totalQuestions (Faza @currentPhase)</p>
        <small>@GetPhaseDescription(currentPhase)</small>
    </div>
    
    @if (currentQuestion != null)
    {
        <div class="question-container">
            <h4>@currentQuestion.QuestionText</h4>
            <div class="answer-buttons">
                <button class="btn btn-success" @onclick="() => SubmitAnswer(true)">Tak</button>
                <button class="btn btn-danger" @onclick="() => SubmitAnswer(false)">Nie</button>
            </div>
        </div>
    }
</div>

@code {
    private DiagnosticQuestion? currentQuestion;
    private Guid sessionId;
    private int currentQuestionNumber = 1;
    private int totalQuestions = 15;
    private int currentPhase = 1;
    
    protected override async Task OnInitializedAsync()
    {
        var session = await DiagnosticService.StartSessionAsync(
            initialSymptoms: symptoms, 
            userId: AuthService.CurrentUserEmail);
        
        sessionId = session.SessionId;
        currentQuestion = session.Questions.FirstOrDefault();
        currentPhase = session.CurrentPhase;
    }
    
    private async Task SubmitAnswer(bool answer)
    {
        var response = await DiagnosticService.SubmitAnswerAsync(
            sessionId, 
            currentQuestion!.Id, 
            answer, 
            AuthService.CurrentUserEmail);
            
        if (response.HasMoreQuestions)
        {
            currentQuestion = response.NextQuestion;
            currentQuestionNumber = response.CurrentQuestion;
            currentPhase = response.CurrentPhase;
            StateHasChanged(); // Ważne dla odświeżenia UI przy zmianie fazy
        }
        else
        {
            // Navigate to recommendations
            NavigationManager.NavigateTo("/recommendations");
        }
    }
    
    private string GetPhaseDescription(int phase) => phase switch
    {
        1 => "Podstawowa ocena - pytania o stan ogólny i konstytucję",
        2 => "Pogłębiona analiza - szczegółowe objawy i wzorce energetyczne", 
        3 => "Specjalistyczna diagnoza - ostateczne ustalenia syndromu TCM",
        _ => "Diagnoza TCM"
    };
}
```

### Recommendations History Component
```csharp
@page "/recommendations"
@inject IRecommendationService RecommendationService
@inject IAuthService AuthService

<h3>My Recommendations</h3>

@if (recommendations?.Items.Any() == true)
{
    @foreach (var recommendation in recommendations.Items)
    {
        <div class="recommendation-card">
            <p><strong>Date:</strong> @recommendation.DateGenerated.ToString("yyyy-MM-dd")</p>
            <p>@recommendation.RecommendationText</p>
            <button @onclick="() => DeleteRecommendation(recommendation.RecommendationId)">
                Delete
            </button>
        </div>
    }
}

@code {
    private PagedResult<RecommendationEntity>? recommendations;
    
    protected override async Task OnInitializedAsync()
    {
        if (AuthService.IsUserAuthenticated)
        {
            recommendations = await RecommendationService.GetUserRecommendationsAsync(
                AuthService.CurrentUserEmail!);
        }
    }
    
    private async Task SaveRecommendation(RecommendationResult recommendationResult)
    {
        if (AuthService.IsUserAuthenticated)
        {
            await RecommendationService.SaveRecommendationAsync(
                AuthService.CurrentUserEmail!, 
                recommendationResult);
            
            // Refresh list
            await OnInitializedAsync();
        }
    }
    
    private async Task DeleteRecommendation(Guid recommendationId)
    {
        await RecommendationService.DeleteRecommendationAsync(
            recommendationId, 
            AuthService.CurrentUserEmail!);
        
        // Refresh list
        await OnInitializedAsync();
    }
}
```

## 7. Validation and Business Logic

### ValidationService Implementation
```csharp
public class ValidationService : IValidationService
{
    public void ValidateSymptoms(string? symptoms)
    {
        if (!string.IsNullOrEmpty(symptoms) && symptoms.Length > 1000)
            throw new ValidationException("Symptoms text cannot exceed 1000 characters");
    }
    
    public void ValidateUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ValidationException("User ID is required");
            
        if (!IsValidEmail(userId))
            throw new ValidationException("User ID must be valid email");
    }
    
    public void ValidateRecommendationText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ValidationException("Recommendation text is required");
            
        if (text.Length > 10000)
            throw new ValidationException("Recommendation text cannot exceed 10,000 characters");
    }
    
    public void ValidatePagination(int page, int limit)
    {
        if (page < 1) throw new ValidationException("Page must be >= 1");
        if (limit < 1 || limit > 50) throw new ValidationException("Limit must be between 1 and 50");
    }
}
```

### Business Logic Rules (Podejście 3-Fazowe)
1. **Diagnostic Sessions (3-Fazowe)**: 
   - Exactly 15 questions per session w 3 fazach (5+5+5)
   - **Faza 1 (pytania 1-5)**: Podstawowe wzorce (Qi, Yang/Yin, konstytucja)
   - **Faza 2 (pytania 6-10)**: Specjalizacja (narządy, systemy, wzorce patologiczne)
   - **Faza 3 (pytania 11-15)**: Finalizacja syndromu TCM (różnicowanie, potwierdzenie)
   - 1-hour session expiry
   - Anonymous sessions allowed
   - **80% redukcja kosztów AI** - tylko 3 wywołania zamiast 15
   - Pytania adaptują się progresywnie na podstawie poprzednich faz

2. **Recommendations**:
   - Authentication required for saving
   - Immutable once saved (no updates allowed)
   - User can only access their own recommendations
   - Soft delete with audit trail

3. **TCM Integration (Ulepszona)**:
   - Progresywna identyfikacja syndromu przez 3 fazy diagnostyczne
   - Complete dietary document with recommendations and weekly meal plan
   - Single formatted text containing both guidelines and meal schedule
   - Lepsze zrozumienie kontekstu przez AI w każdej fazie

## 8. Authentication and Authorization

### AuthService Implementation
```csharp
public class AuthService : IAuthService
{
    private readonly Supabase.Client _supabase;
    private readonly UserContext _userContext;
    
    public bool IsUserAuthenticated => !string.IsNullOrEmpty(CurrentUserEmail);
    public string? CurrentUserEmail => _userContext.Email;
    
    public async Task<AuthResult> ValidateSessionAsync(string accessToken)
    {
        try
        {
            var user = await _supabase.Auth.GetUser(accessToken);
            if (user?.Email != null)
            {
                await SetUserContextAsync(user.Email);
                return new AuthResult { Success = true, UserEmail = user.Email };
            }
        }
        catch (Exception ex)
        {
            return new AuthResult { Success = false, Error = ex.Message };
        }
        
        return new AuthResult { Success = false, Error = "Invalid token" };
    }
    
    public async Task SetUserContextAsync(string userEmail)
    {
        _userContext.Email = userEmail;
        
        // Set RLS context for Supabase
        await _supabase.Rpc("set_config", new Dictionary<string, object>
        {
            ["setting_name"] = "app.current_user_email", 
            ["new_value"] = userEmail,
            ["is_local"] = true
        });
    }
}
```

### Row-Level Security Integration
- User context automatically set for all Supabase operations
- RLS policies enforce data isolation at database level
- No additional authorization checks needed in service layer

## 9. Error Handling and Caching

### Global Error Handling
```csharp
public class GlobalExceptionHandler
{
    public static void HandleException(Exception ex)
    {
        switch (ex)
        {
            case ValidationException validationEx:
                // Show user-friendly validation message
                break;
            case UnauthorizedAccessException:
                // Redirect to login
                break;
            case InvalidOperationException operationEx:
                // Handle business logic errors
                break;
            default:
                // Log error and show generic message
                break;
        }
    }
}
```

### Caching Strategy (Zoptymalizowana dla 3 Faz)
- **Diagnostic Sessions**: In-memory cache, 1-hour expiry (z fazami i historic)
- **User Recommendations**: No caching (real-time data)
- **AI Generated Content**: No caching (personalized responses - 3 fazy)
- **Static Data**: Application-level caching where appropriate
- **Significant Cost Reduction**: 80% mniej wywołań OpenAI dzięki 3-fazowemu podejściu

## 10. Korzyści Podejścia Hybrydowego 3-Fazowego

### Ekonomiczne
- **80% redukcja kosztów AI**: 3 wywołania zamiast 15
- **Lepszy ROI**: Każde wywołanie ma znacznie więcej kontekstu
- **Skalowalne koszty**: Predictable pricing model

### Techniczne
- **Lepsza jakość diagnozy**: Kontekstowe pytania w każdej fazie
- **Skalowalność**: Łatwe dodawanie nowych faz
- **Debugowanie**: Jasne punkty kontrolne w każdej fazie
- **Testowanie**: Możliwość A/B testowania różnych strategii promptów

### UX/Biznesowe
- **Jasna progresja**: Użytkownik rozumie "dlaczego te pytania"
- **Lepsza personalizacja**: Inteligentne adaptowanie ścieżki diagnostycznej
- **Metodologicznie poprawne**: Zgodne z praktykami TCM
- **Poczucie kontroli**: Fazy dają użytkownikowi poczucie celowości

### Medyczne (TCM)
- **Faza 1**: Podstawowe wzorce energetyczne
- **Faza 2**: Specjalizacja według systemów narządowych  
- **Faza 3**: Precyzyjne różnicowanie syndromów
- **Wynik**: Dokładniejsza diagnoza dzięki logicznej progresji 