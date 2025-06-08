# Plan Implementacji Serwisów QiBalance

## 1. Przegląd Architektury Serwisów

Aplikacja QiBalance wykorzystuje architekturę opartą na serwisach w środowisku Blazor Server. Główne serwisy obsługują:
- Sesje diagnostyczne z integracją AI (IDiagnosticService)
- Zarządzanie rekomendacjami użytkowników (IRecommendationService) 
- Autentykację i autoryzację (IAuthService)
- Abstrakcję operacji bazodanowych (ISupabaseService)
- Integrację z OpenAI (IOpenAIService)
- Centralną walidację (IValidationService)

## 2. Szczegóły Implementacji Serwisów

### IDiagnosticService

**Metody:**
- `StartSessionAsync(string? initialSymptoms, string? userId = null)` - rozpoczyna nową sesję diagnostyczną
- `SubmitAnswerAsync(Guid sessionId, string questionId, bool answer, string? userId = null)` - przetwarzanie odpowiedzi
- `IsSessionValidAsync(Guid sessionId)` - walidacja sesji
- `ClearExpiredSessionsAsync()` - czyszczenie wygasłych sesji

**Parametry:**
- Wymagane: sessionId, questionId, answer
- Opcjonalne: initialSymptoms, userId (dla sesji anonimowych)

### IRecommendationService

**Metody:**
- `SaveRecommendationAsync(string userId, RecommendationResult recommendationResult)` - zapis rekomendacji
- `GetUserRecommendationsAsync(string userId, int page = 1, int limit = 10, SortOrder sort = SortOrder.DateDesc)` - pobieranie z paginacją
- `GetRecommendationByIdAsync(Guid recommendationId, string userId)` - pobieranie pojedynczej rekomendacji
- `DeleteRecommendationAsync(Guid recommendationId, string userId)` - usuwanie rekomendacji

**Parametry:**
- Wymagane: userId, recommendationId
- Opcjonalne: page (domyślnie 1), limit (domyślnie 10), sort (domyślnie DateDesc)

### IAuthService

**Metody:**
- `ValidateSessionAsync(string accessToken)` - walidacja tokenu dostępu
- `SetUserContextAsync(string userEmail)` - ustawienie kontekstu użytkownika
- `ClearUserContextAsync()` - czyszczenie kontekstu

**Właściwości:**
- `IsUserAuthenticated` - status autentykacji
- `CurrentUserEmail` - email bieżącego użytkownika

## 3. Wykorzystywane Typy

### DTOs Core:
- `DiagnosticSession` - aktywna sesja diagnostyczna z pytaniami i odpowiedziami (z fazami)
- `DiagnosticQuestion` - pojedyncze pytanie diagnostyczne
- `DiagnosticAnswer` - odpowiedź użytkownika na pytanie
- `DiagnosticResponse` - odpowiedź serwisu na submitowanie odpowiedzi (z fazą)
- `DiagnosticPhase` - grupa pytań dla konkretnej fazy diagnostycznej
- `RecommendationResult` - wynik generowania rekomendacji przez AI
- `RecommendationEntity` - encja rekomendacji dla UI

### DTOs Supporting:
- `PagedResult<T>` - wyniki z paginacją
- `AuthResult` - wynik walidacji autentykacji
- `UserContext` - kontekst użytkownika w sesji
- `StartDiagnosticRequest` - request do rozpoczęcia diagnozy
- `SubmitAnswerRequest` - request submitowania odpowiedzi
- `SaveRecommendationRequest` - request zapisu rekomendacji

### Database Models:
- `Recommendation` - model bazy danych dla rekomendacji
- `RecommendationInsert` - DTO dla insertu
- `RecommendationUpdate` - DTO dla update'u

## 4. Przepływ Danych

### Sesja Diagnostyczna (Podejście Hybrydowe - 3 Fazy):
1. **Faza 1**: Komponent wywołuje `DiagnosticService.StartSessionAsync()`
   - DiagnosticService wywołuje `OpenAIService.GeneratePhaseQuestionsAsync(1, initialSymptoms, [])`
   - Zwraca 5 pytań bazowych, sesja cachowana w IMemoryCache
2. **Faza 2**: Po 5 odpowiedziach, automatycznie generuje następną fazę
   - DiagnosticService wywołuje `OpenAIService.GeneratePhaseQuestionsAsync(2, initialSymptoms, answers_1_5)`
   - Dodaje 5 pytań średnio-zaawansowanych do sesji
3. **Faza 3**: Po 10 odpowiedziach, automatycznie generuje ostatnią fazę
   - DiagnosticService wywołuje `OpenAIService.GeneratePhaseQuestionsAsync(3, initialSymptoms, answers_1_10)`
   - Dodaje 5 pytań specjalistycznych do sesji
4. **Finał**: Po wszystkich 15 odpowiedziach generowane są rekomendacje
   - DiagnosticService wywołuje `OpenAIService.GenerateRecommendationsAsync(initialSymptoms, allAnswers)`

### Zarządzanie Rekomendacjami:
1. Komponent wywołuje `RecommendationService.SaveRecommendationAsync()`
2. ValidationService waliduje dane wejściowe
3. SupabaseService wykonuje operację INSERT z RLS
4. Zwracana jest encja RecommendationEntity

### Autentykacja:
1. AuthService waliduje token z Supabase
2. Ustawia kontekst użytkownika w UserContext
3. Konfiguruje RLS context w Supabase
4. Wszystkie operacje bazodanowe używają RLS automatycznie

## 5. Względy Bezpieczeństwa

### Row-Level Security (RLS):
- Wszystkie tabele z user_id mają włączone RLS
- Polityki zapewniają dostęp tylko do własnych danych
- Kontekst użytkownika ustawiany przed każdą operacją

### Walidacja Danych:
- Sanityzacja wszystkich inputów przed wysłaniem do AI
- Ograniczenia długości tekstu (objawy: 1000 znaków, rekomendacje: 10000)
- Walidacja formatu email dla userId
- Walidacja zakresu paginacji

### Autoryzacja:
- DiagnosticService: sesje anonimowe dozwolone
- RecommendationService: wymaga autentykacji
- AuthService: zarządza stanem autentykacji
- Wszystkie operacje CRUD wymagają userId

## 6. Obsługa Błędów

### Typy Wyjątków:
- `ValidationException` - nieprawidłowe dane wejściowe
- `UnauthorizedAccessException` - brak dostępu
- `InvalidOperationException` - nieprawidłowy stan (wygasła sesja)
- `Exception` - błędy systemowe

### Scenariusze Błędów:
- **Wygasła sesja diagnostyczna**: InvalidOperationException
- **Nieprawidłowy userId**: ValidationException
- **Zbyt długi tekst**: ValidationException
- **Nieautoryzowany dostęp**: UnauthorizedAccessException
- **Błąd AI**: Exception z logowaniem
- **Błąd bazy danych**: Exception z retry logic

### Globalna Obsługa:
```csharp
public class GlobalExceptionHandler
{
    public static void HandleException(Exception ex)
    {
        switch (ex)
        {
            case ValidationException:
                // Pokaż przyjazny komunikat walidacji
                break;
            case UnauthorizedAccessException:
                // Przekieruj na login
                break;
            case InvalidOperationException:
                // Obsłuż błędy logiki biznesowej
                break;
            default:
                // Loguj i pokaż ogólny komunikat
                break;
        }
    }
}
```

## 7. Rozważania dotyczące Wydajności

### Strategia Cachowania:
- **Sesje Diagnostyczne**: IMemoryCache, 1-godzinny TTL (z fazami)
- **Rekomendacje Użytkowników**: Brak cache (dane real-time)
- **Treść AI**: Brak cache (spersonalizowane odpowiedzi - 3 fazy)
- **Dane Statyczne**: Cache na poziomie aplikacji

### Optymalizacje (Podejście Hybrydowe):
- **Znacznie mniej wywołań AI**: Tylko 3 wywołania zamiast 15 (oszczędność ~80% kosztów)
- Lazy loading rekomendacji z paginacją
- Async/await dla wszystkich operacji I/O
- Connection pooling w Supabase
- Batch clearing wygasłych sesji
- **Inteligentne generowanie pytań**: Każda faza wykorzystuje odpowiedzi z poprzednich

### Monitoring:
- Logowanie czasów odpowiedzi AI
- Metryki wykorzystania cache
- Monitoring błędów autentykacji

## 8. Etapy Implementacji

### Faza 1: Infrastruktura Podstawowa
1. **Implementacja IValidationService**
   - Metody walidacji dla wszystkich typów danych
   - Centralne komunikaty błędów
   - Unit testy walidacji

2. **Implementacja ISupabaseService**
   - Generic CRUD operations
   - RLS integration
   - Connection management
   - Error handling

3. **Konfiguracja Dependency Injection**
   - Rejestracja wszystkich serwisów
   - Konfiguracja Semantic Kernel
   - Setup IMemoryCache
   - Konfiguracja Supabase Client

### Faza 2: Serwisy Autentykacji
4. **Implementacja IAuthService**
   - Walidacja tokenów Supabase
   - Zarządzanie UserContext
   - RLS context setup
   - Session management

5. **Implementacja UserContext**
   - Scoped service dla kontekstu użytkownika
   - Integration z AuthService
   - State management

### Faza 3: Serwisy AI i Diagnozy
6. **Implementacja IOpenAIService (Podejście Hybrydowe)**
   - Metoda `GeneratePhaseQuestionsAsync(int phase, string? initialSymptoms, List<DiagnosticAnswer> previousAnswers)`
   - Prompt engineering dla 3 faz TCM (bazowe, średnio-zaawansowane, specjalistyczne)
   - JSON response parsing dla DiagnosticPhase
   - Error handling dla AI calls i rate limiting

7. **Implementacja IDiagnosticService (3-Fazowy)**
   - Session management w cache z fazami
   - Logic 3-fazowego generowania pytań (co 5 pytań nowa faza)
   - Automatyczne przechodzenie między fazami
   - Integration z OpenAIService dla każdej fazy
   - Expired sessions cleanup

### Faza 4: Serwisy Rekomendacji
8. **Implementacja IRecommendationService**
   - CRUD operations
   - Pagination logic
   - User authorization checks
   - Data mapping między DTOs

### Faza 5: Testy i Optymalizacja
9. **Unit Testing**
   - Testy wszystkich serwisów
   - Mock dependencies
   - Edge cases testing
   - Performance benchmarks

10. **Integration Testing**
    - End-to-end scenariusze
    - Database integration
    - AI service integration
    - Authentication flows

11. **Performance Optimization**
    - Cache tuning
    - Query optimization
    - Connection pooling
    - Memory management

### Faza 6: Monitoring i Logging
12. **Implementacja Loggingu**
    - Structured logging z Serilog
    - Performance metrics
    - Error tracking
    - User activity monitoring

13. **Health Checks**
    - Database connectivity
    - AI service availability
    - Cache health
    - Authentication service status

## 9. Konfiguracja w Program.cs

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

// Caching and State
builder.Services.AddMemoryCache();
builder.Services.AddScoped<UserContext>();

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<AIServiceHealthCheck>("ai-service");
```

## 10. Przykłady Użycia w Komponentach

### Diagnostic Component (3-Fazowy):
```csharp
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

    private async Task StartDiagnosis()
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
            StateHasChanged(); // Refresh UI for phase changes
        }
        else
        {
            // Navigate to recommendations
            NavigationManager.NavigateTo("/recommendations");
        }
    }
    
    private string GetPhaseDescription(int phase) => phase switch
    {
        1 => "Podstawowa ocena - pytania o stan ogólny",
        2 => "Pogłębiona analiza - szczegółowe objawy", 
        3 => "Specjalistyczna diagnoza - ostateczne ustalenia",
        _ => "Diagnoza"
    };
}
```

### Recommendations Component:
```csharp
@inject IRecommendationService RecommendationService
@inject IAuthService AuthService

private async Task LoadRecommendations()
{
    if (AuthService.IsUserAuthenticated)
    {
        recommendations = await RecommendationService.GetUserRecommendationsAsync(
            AuthService.CurrentUserEmail!, 
            page: currentPage,
            limit: 10);
    }
}
```

## 11. Szczegóły Implementacji Podejścia Hybrydowego

### DTOs dla Podejścia 3-Fazowego

```csharp
// Rozszerzona DiagnosticSession
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

// Nowy DTO dla fazy
public class DiagnosticPhase
{
    public int Phase { get; set; }
    public string PhaseName { get; set; } = string.Empty;
    public string PhaseDescription { get; set; } = string.Empty;
    public List<DiagnosticQuestion> Questions { get; set; } = new();
}

// Rozszerzony DiagnosticResponse
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

### Zaktualizowany IOpenAIService

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

### Logika 3-Fazowa w DiagnosticService

- **Faza 1 (Start)**: Generuje 5 pytań bazowych na podstawie objawów początkowych
- **Faza 2 (Po pytaniu 5)**: Generuje 5 pytań średnio-zaawansowanych na podstawie odpowiedzi 1-5
- **Faza 3 (Po pytaniu 10)**: Generuje 5 pytań specjalistycznych na podstawie odpowiedzi 1-10
- **Finalizacja (Po pytaniu 15)**: Generuje rekomendacje na podstawie wszystkich odpowiedzi

### Korzyści Biznesowe

1. **Redukcja Kosztów AI**: 80% mniej wywołań OpenAI
2. **Lepsza Personalizacja**: Pytania adaptują się progresywnie
3. **Lepszy UX**: Użytkownik rozumie progresję diagnozy
4. **Optymalna Diagnostyka TCM**: Logiczna progresja od ogólnych do specjalistycznych objawów

Ten plan zapewnia kompleksowe wytyczne dla implementacji wszystkich serwisów w aplikacji QiBalance z nowym podejściem hybrydowym, uwzględniając bezpieczeństwo, wydajność i łatwość konserwacji. 