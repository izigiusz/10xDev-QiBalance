# Plan Przepływu Pytań Diagnostycznych - Podejście Hybrydowe

## Rekomendowane Podejście: Adaptacyjne Grupy Pytań

### Koncepcja
- **Faza 1**: 5 pytań bazowych generowanych na podstawie objawów początkowych
- **Faza 2**: 5 pytań średnio-zaawansowanych na podstawie pierwszych 5 odpowiedzi
- **Faza 3**: 5 pytań specjalistycznych na podstawie wszystkich poprzednich odpowiedzi

### Zalety Tego Podejścia
1. **Personalizacja**: Pytania adaptują się do odpowiedzi użytkownika
2. **Efektywność**: Tylko 3 wywołania API zamiast 15
3. **Balans kosztów**: Znacznie tańsze niż dynamiczne, ale bardziej zaawansowane niż statyczne
4. **UX**: Użytkownik widzi postęp w grupach po 5 pytań

## Implementacja

### 1. Zmodyfikowane DTOs

```csharp
// Rozszerzona DiagnosticSession z fazami
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

// Nowy DTO dla grupy pytań w fazie
public class DiagnosticPhase
{
    public int Phase { get; set; }
    public string PhaseName { get; set; } = string.Empty;
    public string PhaseDescription { get; set; } = string.Empty;
    public List<DiagnosticQuestion> Questions { get; set; } = new();
}

// Rozszerzony DiagnosticResponse z informacją o fazie
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

### 2. Zmodyfikowane IOpenAIService

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

### 3. Zmodyfikowane DiagnosticService

```csharp
public class DiagnosticService : IDiagnosticService
{
    public async Task<DiagnosticSession> StartSessionAsync(string? initialSymptoms, string? userId = null)
    {
        // Walidacja
        _validationService.ValidateSymptoms(initialSymptoms);
        
        // Generuj pierwszą fazę pytań (5 pytań bazowych)
        var phase1 = await _openAIService.GeneratePhaseQuestionsAsync(1, initialSymptoms, new List<DiagnosticAnswer>());
        
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
        
        _cache.Set($"diagnostic_session_{session.SessionId}", session, TimeSpan.FromHours(1));
        return session;
    }
    
    public async Task<DiagnosticResponse> SubmitAnswerAsync(Guid sessionId, string questionId, bool answer, string? userId = null)
    {
        var session = _cache.Get<DiagnosticSession>($"diagnostic_session_{sessionId}");
        if (session == null || session.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("Session expired or not found");
            
        // Dodaj odpowiedź
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

### 4. Przykładowe Prompty dla OpenAI

#### Faza 1 - Pytania Bazowe
```
Jesteś ekspertem medycyny chińskiej (TCM). Wygeneruj 5 pytań bazowych (tak/nie) dla diagnozy TCM.
Objawy początkowe: {initialSymptoms}

Zwróć JSON:
{
  "phase": 1,
  "phaseName": "Podstawowa ocena",
  "phaseDescription": "Pytania dotyczące podstawowych objawów i stanu ogólnego",
  "questions": [
    {"id": "q1_1", "questionText": "Czy odczuwasz często zimno?", "questionType": 0},
    // ... 4 więcej
  ]
}
```

#### Faza 2 - Pytania Średnio-zaawansowane
```
Jesteś ekspertem medycyny chińskiej (TCM). Na podstawie odpowiedzi z fazy 1, wygeneruj 5 pytań średnio-zaawansowanych (tak/nie) dla diagnozy TCM.

Objawy początkowe: {initialSymptoms}

Odpowiedzi z fazy 1:
{foreach answer in phase1Answers}
- {answer.Question}: {answer.Answer ? "Tak" : "Nie"}
{end}

Na podstawie tych odpowiedzi, skoncentruj się na szczegółach dotyczących:
- Konkretnych systemów narządowych
- Wzorców energetycznych (Qi, Blood, Yin/Yang)
- Czynników patogennych

Zwróć JSON:
{
  "phase": 2,
  "phaseName": "Pogłębiona analiza",
  "phaseDescription": "Pytania o szczegółowe objawy i wzorce energetyczne",
  "questions": [
    {"id": "q2_1", "questionText": "Czy odczuwasz suchość w ustach nocą?", "questionType": 0},
    // ... 4 więcej
  ]
}
```

#### Faza 3 - Pytania Specjalistyczne
```
Jesteś ekspertem medycyny chińskiej (TCM). Na podstawie wszystkich dotychczasowych odpowiedzi, wygeneruj 5 końcowych pytań specjalistycznych (tak/nie) dla postawienia precyzyjnej diagnozy TCM.

Objawy początkowe: {initialSymptoms}

Wszystkie dotychczasowe odpowiedzi:
{foreach answer in allAnswers}
- {answer.Question}: {answer.Answer ? "Tak" : "Nie"}
{end}

Na podstawie całości odpowiedzi, skoncentruj się na:
- Ostatecznym określeniu syndromu TCM
- Różnicowaniu między podobnymi wzorcami
- Najważniejszych aspektach do potwierdzenia diagnozy

Zwróć JSON:
{
  "phase": 3,
  "phaseName": "Specjalistyczna diagnoza",
  "phaseDescription": "Pytania kluczowe dla ostatecznej diagnozy TCM",
  "questions": [
    {"id": "q3_1", "questionText": "Czy ból nasila się przy nacisku?", "questionType": 0},
    // ... 4 więcej
  ]
}
```

#### Generowanie Finalnych Rekomendacji
```
Jesteś ekspertem medycyny chińskiej (TCM). Na podstawie kompletnej diagnozy wygeneruj szczegółowe rekomendacje dietetyczne zgodne z zasadami TCM.

Objawy początkowe: {initialSymptoms}

Kompletne odpowiedzi diagnostyczne (15 pytań):
{foreach answer in allAnswers}
- {answer.Question}: {answer.Answer ? "Tak" : "Nie"}
{end}

Na podstawie tej diagnozy:
1. Określ główny syndrom TCM
2. Wygeneruj szczegółowe rekomendacje dietetyczne
3. Zaproponuj tygodniowy plan posiłków
4. Wskaż produkty do unikania

Zwróć JSON:
{
  "recommendationText": "Kompleksowy dokument z rekomendacjami dietetycznymi, planem posiłków na tydzień i produktami do unikania",
  "tcmSyndrome": "Nazwa zidentyfikowanego syndromu TCM"
}
```

### 5. UI Components Update

```csharp
@page "/diagnostic"
@inject IDiagnosticService DiagnosticService

<div class="diagnostic-container">
    <div class="progress-container">
        <div class="progress-bar">
            <div class="progress-fill" style="width: @(((float)currentQuestion / totalQuestions) * 100)%"></div>
        </div>
        <p>Pytanie @currentQuestion z @totalQuestions (Faza @currentPhase)</p>
    </div>
    
    @if (currentQuestion != null)
    {
        <div class="question-container">
            <h4>@GetPhaseDescription(currentPhase)</h4>
            <p class="question-text">@currentQuestion.QuestionText</p>
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
    private int currentQuestion = 1;
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

## Korzyści Tego Podejścia

### 1. **Drastyczna Optymalizacja Kosztów**
- **80% redukcja kosztów**: Tylko 3 wywołania OpenAI zamiast 15
- Każde wywołanie ma znacznie więcej kontekstu, więc wyniki są lepsze
- Większy ROI z każdego zapytania do AI

### 2. **Zaawansowana Personalizacja**
- Pytania w fazie 2 są dostosowane do odpowiedzi z fazy 1
- Pytania w fazie 3 wykorzystują całą historię odpowiedzi
- Progresywna specjalizacja - od ogólnych do bardzo konkretnych
- Inteligentne adaptowanie ścieżki diagnostycznej

### 3. **Znacznie Lepszy UX**
- Użytkownik widzi jasny postęp w fazach
- Zrozumienie "dlaczego akurat te pytania teraz"
- Możliwość różnych interfejsów dla różnych faz
- Poczucie progresji i celowości diagnozy

### 4. **Optymalna Diagnostyka TCM**
- **Faza 1**: Podstawowe wzorce (Qi, Yang/Yin, konstytucja, energia)
- **Faza 2**: Specjalizacja (konkretne narządy, systemy, wzorce patologiczne)
- **Faza 3**: Finalizacja syndromu TCM (różnicowanie, potwierdzenie diagnozy)
- Logiczny przepływ zgodny z metodologią TCM

### 5. **Łatwość Rozwoju i Testowania**
- Każdą fazę można rozwijać i testować niezależnie
- Jasne punkty kontrolne w diagnostyce
- Możliwość A/B testowania różnych strategii promptów
- Łatwe debugowanie i optymalizacja każdej fazy

### 6. **Skalowalność i Elastyczność**
- Łatwe dodawanie nowych faz w przyszłości
- Możliwość dostosowania liczby pytań w każdej fazie
- Elastyczna architektura dla różnych typów diagnoz
- Gotowość na integrację z dodatkowymi źródłami danych

### 7. **Jakość Diagnozy**
- Lepsze zrozumienie kontekstu przez AI w każdej fazie
- Bardziej precyzyjne pytania dzięki wykorzystaniu poprzednich odpowiedzi
- Wyższa dokładność końcowej diagnozy TCM
- Mniejsze ryzyko błędnych ścieżek diagnostycznych

## Szczegóły Implementacyjne

### Timing Faz
- **Faza 1**: Pytania 1-5 (Start sesji)
- **Faza 2**: Pytania 6-10 (Po 5 odpowiedziach)
- **Faza 3**: Pytania 11-15 (Po 10 odpowiedziach)
- **Finalizacja**: Generowanie rekomendacji (Po wszystkich 15 odpowiedziach)

### Logika Przejść Między Fazami
```csharp
// W SubmitAnswerAsync
if (session.CurrentQuestion == 6) 
{
    session.CurrentPhase = 2;
    // Generuj fazę 2 na podstawie odpowiedzi 1-5
}
else if (session.CurrentQuestion == 11) 
{
    session.CurrentPhase = 3;
    // Generuj fazę 3 na podstawie odpowiedzi 1-10
}
```

### Struktura Promptów TCM
1. **Faza 1**: Podstawowe pytania o energię, konstytucję, ogólny stan
2. **Faza 2**: Szczegółowe pytania o systemy narządowe na podstawie fazy 1
3. **Faza 3**: Precyzyjne pytania różnicujące podobne syndromy
4. **Rekomendacje**: Kompletny plan dietetyczny z tygodniowym harmonogramem

### Wskaźniki Sukcesu
- **Redukcja kosztów**: 80% mniej wywołań API
- **Lepsze UX**: Jasne fazy progresji
- **Wyższa jakość**: Kontekstowe pytania w każdej fazie
- **Skalowalność**: Łatwe dodawanie nowych faz

## Podsumowanie

To podejście hybrydowe 3-fazowe zapewnia optymalny balans między:
- **Kosztami a jakością**: 80% oszczędności przy lepszych wynikach
- **Personalizacją a efektywnością**: Inteligentne pytania bez zbędnych wywołań
- **Dokładnością TCM a UX**: Metodologicznie poprawny przepływ z doskonałym doświadczeniem użytkownika

Plan jest gotowy do implementacji i zapewnia solidne fundamenty dla systemu diagnostycznego QiBalance. 