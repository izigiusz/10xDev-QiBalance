# Plan implementacji widoku diagnostycznego

## 1. Przegląd
Widok diagnostyczny to kluczowy komponent aplikacji QiBalance, odpowiedzialny za przeprowadzenie 3-fazowej diagnozy TCM składającej się z 15 pytań (5+5+5). Komponent implementuje innowacyjne podejście hybrydowe, które redukuje koszty AI o 80% poprzez progresywne generowanie pytań w trzech fazach: podstawowej oceny, pogłębionej analizy i specjalistycznej diagnozy. Widok obsługuje zarówno użytkowników anonimowych, jak i zalogowanych, zapewniając płynne doświadczenie diagnostyczne z jasną wizualizacją postępu.

## 2. Routing widoku
Ścieżka: `/diagnostic`
- Dostępna dla wszystkich użytkowników (z i bez uwierzytelnienia)
- Wymaga parametru `initialSymptoms` przekazanego z widoku głównego
- Automatyczne przekierowanie do `/recommendations` po zakończeniu diagnozy
- Przekierowanie do `/` w przypadku wygaśnięcia sesji

## 3. Struktura komponentów
```
DiagnosticComponent (główny widok)
├── DiagnosticProgressBarComponent (postęp diagnozy)
├── PhaseIndicatorComponent (opis aktualnej fazy)
├── DiagnosticCardComponent (kontener pytania)
│   ├── QuestionTextDisplay (tekst pytania)
│   └── YesNoButtonsComponent (przyciski odpowiedzi)
├── LoadingIndicatorComponent (wskaźnik ładowania)
└── ErrorDisplayComponent (obsługa błędów)
```

## 4. Szczegóły komponentów

### DiagnosticComponent
- **Opis komponentu**: Główny komponent zarządzający całym przepływem 3-fazowej diagnozy TCM. Odpowiada za inicjalizację sesji, obsługę odpowiedzi użytkownika, przejścia między fazami oraz finalizację diagnozy.
- **Główne elementy**: Kontener Bootstrap z progress bar, wskaźnikiem fazy, kartą pytania i przyciskami odpowiedzi. Layout responsywny z centrum-wyrównanym contentem.
- **Obsługiwane zdarzenia**: OnInitializedAsync (start sesji), SubmitAnswer (przesyłanie odpowiedzi), OnPhaseTransition (przejście między fazami), OnDiagnosisComplete (zakończenie)
- **Warunki walidacji**: Sesja musi być aktywna i nie wygasła (sprawdzenie co 30s), questionId musi być prawidłowy, odpowiedź musi być typu bool, maksymalny czas sesji 1 godzina
- **Typy**: DiagnosticViewModel, DiagnosticSession, DiagnosticResponse, DiagnosticQuestion
- **Propsy**: initialSymptoms (string?), userId (string? z AuthService)

### DiagnosticProgressBarComponent
- **Opis komponentu**: Responsywny wskaźnik postępu wyświetlający aktualny numer pytania, fazę diagnozy i postęp procentowy. Zapewnia użytkownikowi jasne zrozumienie miejsca w procesie diagnostycznym.
- **Główne elementy**: Bootstrap progress bar z animacją, tekstowe etykiety postępu w formacie "Pytanie X z 15 (Faza Y)", responsive layout
- **Obsługiwane zdarzenia**: Brak (komponent tylko wyświetlający)
- **Warunki walidacji**: currentQuestion w zakresie 1-15, currentPhase w zakresie 1-3, totalQuestions = 15
- **Typy**: int currentQuestion, int totalQuestions, int currentPhase
- **Propsy**: CurrentQuestion, TotalQuestions, CurrentPhase

### PhaseIndicatorComponent
- **Opis komponentu**: Wyświetla kontekstowy opis aktualnej fazy diagnostycznej, pomagając użytkownikowi zrozumieć cel pytań w danej fazie zgodnie z metodologią TCM.
- **Główne elementy**: Bootstrap card z ikoną fazy, tytułem fazy i opisem, kolorystyka dostosowana do fazy
- **Obsługiwane zdarzenia**: Brak (komponent tylko wyświetlający)  
- **Warunki walidacji**: currentPhase w zakresie 1-3, phaseDescription nie może być pusty
- **Typy**: int currentPhase, string phaseDescription
- **Propsy**: CurrentPhase, PhaseDescription

### DiagnosticCardComponent
- **Opis komponentu**: Główny kontener dla wyświetlania pytania diagnostycznego z opcjami odpowiedzi. Implementuje semantic HTML dla dostępności i responsive design.
- **Główne elementy**: Bootstrap card z header (numer pytania), body (tekst pytania), footer (przyciski odpowiedzi), loading state overlay
- **Obsługiwane zdarzenia**: OnAnswerSelected (przekazanie odpowiedzi do rodzica)
- **Warunki walidacji**: currentQuestion nie może być null, questionText nie może być pusty, isLoading zapobiega wielokrotnym kliknięciom
- **Typy**: DiagnosticQuestion?, bool isLoading, EventCallback<bool> OnAnswerSelected
- **Propsy**: CurrentQuestion, IsLoading, OnAnswerSelected

### YesNoButtonsComponent
- **Opis komponentu**: Para przycisków Tak/Nie z obsługą stanów disabled, loading i focus management. Zapewnia dostępność przez keyboard navigation.
- **Główne elementy**: Dwa Bootstrap przyciski (btn-success, btn-danger) w flexbox layout, ARIA labels, keyboard support
- **Obsługiwane zdarzenia**: OnYesClick, OnNoClick z debouncing
- **Warunki walidacji**: isDisabled zapobiega wielokrotnym kliknięciom, przyciski dostępne tylko gdy pytanie jest załadowane
- **Typy**: bool isDisabled, EventCallback<bool> OnAnswerClick
- **Propsy**: IsDisabled, OnAnswerClick

## 5. Typy

### DiagnosticViewModel (nowy typ)
```csharp
public class DiagnosticViewModel
{
    public Guid SessionId { get; set; }
    public DiagnosticQuestion? CurrentQuestion { get; set; }
    public int CurrentQuestionNumber { get; set; } = 1;
    public int TotalQuestions { get; set; } = 15;
    public int CurrentPhase { get; set; } = 1;
    public string PhaseDescription { get; set; } = string.Empty;
    public bool IsLoading { get; set; } = false;
    public bool HasError { get; set; } = false;
    public string? ErrorMessage { get; set; }
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
}
```

### DiagnosticUIState (nowy typ)
```csharp
public class DiagnosticUIState  
{
    public bool IsSubmittingAnswer { get; set; } = false;
    public bool IsSessionExpired { get; set; } = false;
    public bool IsTransitioningPhase { get; set; } = false;
    public string? LastError { get; set; }
    public DateTime SessionStartTime { get; set; } = DateTime.UtcNow;
}
```

**Istniejące typy z DTOs**: DiagnosticSession, DiagnosticResponse, DiagnosticQuestion, DiagnosticAnswer, DiagnosticPhase

## 6. Zarządzanie stanem
Stan widoku zarządzany jest lokalnie w komponencie DiagnosticComponent przy użyciu:
- **diagnosticViewModel**: DiagnosticViewModel - główny stan komponentu z aktualnym pytaniem i postępem
- **diagnosticUIState**: DiagnosticUIState - stan UI (loading, errory, transitions)
- **sessionTimer**: Timer - sprawdzanie ważności sesji co 30 sekund
- **StateHasChanged()**: wywołane przy zmianach fazy dla odświeżenia UI

**Cykl życia stanu**:
1. OnInitializedAsync: Inicjalizacja sesji przez StartSessionAsync
2. SubmitAnswer: Aktualizacja stanu po każdej odpowiedzi
3. PhaseTransition: Odświeżenie UI przy przejściu między fazami (pytania 5, 10)
4. SessionValidation: Periodyczne sprawdzanie ważności sesji

## 7. Integracja API

### Wywołania serwisów:
1. **DiagnosticService.StartSessionAsync(initialSymptoms, userId)**
   - Request: string? initialSymptoms, string? userId
   - Response: DiagnosticSession
   - Użycie: Inicjalizacja sesji diagnostycznej z pierwszą fazą pytań

2. **DiagnosticService.SubmitAnswerAsync(sessionId, questionId, answer, userId)**
   - Request: Guid sessionId, string questionId, bool answer, string? userId  
   - Response: DiagnosticResponse
   - Użycie: Przesyłanie odpowiedzi i otrzymanie następnego pytania lub wyników

3. **DiagnosticService.IsSessionValidAsync(sessionId)**
   - Request: Guid sessionId
   - Response: bool
   - Użycie: Sprawdzanie ważności sesji (timer co 30s)

### Obsługa automatycznych przejść faz:
- Faza 1→2: Automatycznie po 5 odpowiedzi (currentQuestion = 6)
- Faza 2→3: Automatycznie po 10 odpowiedzi (currentQuestion = 11)  
- Finalizacja: Po 15 odpowiedzi przekierowanie do `/recommendations`

## 8. Interakcje użytkownika

### Podstawowe interakcje:
1. **Kliknięcie "Tak"**: Wywołanie SubmitAnswer(true), ustawienie loading state, oczekiwanie na odpowiedź API
2. **Kliknięcie "Nie"**: Wywołanie SubmitAnswer(false), analogiczny przepływ jak "Tak"
3. **Przejście między fazami**: Automatyczne po 5 i 10 pytaniach, wyświetlenie animacji przejścia
4. **Wygaśnięcie sesji**: Toast notification + przekierowanie do `/`

### Stany interfejsu:
- **Loading**: Przyciski disabled, spinner widoczny
- **Active**: Przyciski enabled, pytanie wyświetlone
- **Phase Transition**: Krótka animacja + aktualizacja progress bara
- **Error**: Error message + opcja retry lub powrót do home

### Accessibility:
- Keyboard navigation (Tab, Enter, Space)
- ARIA live regions dla dynamicznych aktualizacji
- Focus management przy zmianie pytań
- Screen reader announcements dla przejść faz

## 9. Warunki i walidacja

### Walidacja na poziomie komponentów:
- **DiagnosticComponent**: Sprawdzenie ważności sessionId przed każdym SubmitAnswer, walidacja currentQuestion w zakresie 1-15
- **DiagnosticCardComponent**: Walidacja czy currentQuestion != null przed wyświetleniem
- **YesNoButtonsComponent**: Zapobieganie wielokrotnym kliknięciom przez isDisabled
- **DiagnosticProgressBarComponent**: Walidacja zakresów currentQuestion (1-15), currentPhase (1-3)

### Warunki biznesowe:
- Sesja ważna maksymalnie 1 godzinę
- Dokładnie 15 pytań w 3 fazach (5+5+5)
- Pytania generowane progresywnie (faza 2 po pytaniu 5, faza 3 po pytaniu 10)
- Użytkownicy anonimowi mogą korzystać z diagnozy, ale nie mogą zapisać wyników

### Walidacja serwisów:
- SessionId musi istnieć w cache i być niewyga sły
- QuestionId musi być prawidłowy dla aktualnej sesji
- Answer musi być typu bool
- UserId opcjonalny, ale wymagany do zapisu rekomendacji

## 10. Obsługa błędów

### Scenariusze błędów i ich obsługa:
1. **InvalidOperationException (wygasła sesja)**:
   - Toast notification: "Sesja diagnostyczna wygasła"
   - Automatyczne przekierowanie do `/` po 3 sekundach
   - Czyszczenie stanu lokalnego

2. **NetworkException (błąd sieci)**:
   - Toast notification z możliwością retry
   - Przycisk "Spróbuj ponownie" zachowuje aktualny stan
   - Offline indicator w UI

3. **ValidationException (błędne dane)**:
   - Error message w komponencie
   - Możliwość poprawy lub restartu sesji
   - Logging błędu dla diagnostyki

4. **Timeout API**:
   - Loading state z timeout (30s)
   - Opcja anulowania i powrotu do poprzedniego stanu
   - Automatyczny retry (max 3 próby)

### Globalna obsługa błędów:
- Exception handling w try-catch blocks
- Structured logging z context
- Graceful degradation - powrót do bezpiecznego stanu
- User-friendly error messages w języku polskim

## 11. Kroki implementacji

1. **Utworzenie podstawowej struktury komponentu DiagnosticComponent**
   - Implementacja podstawowego routingu i layout
   - Dodanie dependency injection dla serwisów
   - Utworzenie podstawowych typów ViewModel

2. **Implementacja DiagnosticProgressBarComponent**
   - Bootstrap progress bar z animacjami
   - Responsywny design dla wszystkich urządzeń
   - Testy jednostkowe dla kalkulacji procentów

3. **Implementacja PhaseIndicatorComponent**
   - Mapping faz do opisów zgodnie z TCM
   - Stylizacja z Bootstrap cards
   - Accessibility features (ARIA labels)

4. **Implementacja DiagnosticCardComponent**
   - Semantic HTML structure
   - Loading states i error handling
   - Integration z parent component

5. **Implementacja YesNoButtonsComponent**
   - Keyboard navigation support
   - Debouncing dla zapobiegania wielokrotnym kliknięciom
   - ARIA accessibility features

6. **Integracja API i zarządzanie stanem**
   - Połączenie z DiagnosticService
   - Implementacja session management
   - Error handling dla wszystkich API calls

7. **Implementacja 3-fazowego przepływu**
   - Logika przejść między fazami
   - Animacje i transitions
   - State management podczas przejść

8. **Obsługa błędów i edge cases**
   - Session expiration handling
   - Network error recovery
   - Validation error displays

9. **Stylizacja i responsywność**
   - Bootstrap responsive utilities
   - Custom CSS dla animacji
   - Mobile-first approach

10. **Testowanie i optymalizacja**
    - Unit tests dla wszystkich komponentów
    - Integration tests dla API calls
    - Performance testing dla długich sesji
    - Accessibility testing z screen readers

11. **Dokumentacja i finalizacja**
    - Code documentation
    - User experience testing
    - Performance monitoring setup
    - Production deployment verification 