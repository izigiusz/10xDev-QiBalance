# Architektura UI dla QiBalance

## 1. Przegląd struktury UI

Aplikacja QiBalance MVP wykorzystuje architekturę Blazor Server z czterema głównymi widokami w sekwencyjnym przepływie diagnostycznym. Interfejs jest zbudowany w oparciu o Blazor.Bootstrap z domyślnymi stylami dla zachowania spójności wizualnej. Architektura wspiera zarówno użytkowników anonimowych (dla generowania rekomendacji), jak i zalogowanych (z możliwością zapisu i dostępu do historii).

Kluczową cechą architektury jest 3-fazowy przepływ diagnostyczny (5+5+5 pytań) zintegrowany z OpenAI API, zapewniający progresywną personalizację pytań przy jednoczesnej optymalizacji kosztów o 80%. System wykorzystuje IMemoryCache dla zarządzania stanem sesji oraz Row-Level Security (RLS) dla bezpieczeństwa danych.

## 2. Lista widoków

### HomeComponent (/)
- **Główny cel**: Zbieranie objawów początkowych i rozpoczęcie sesji diagnostycznej
- **Kluczowe informacje**: Formularz wprowadzania objawów, informacje o aplikacji, opcje logowania
- **Kluczowe komponenty**:
  - `SymptomsInputComponent`: TextArea z walidacją 1000 znaków i licznikiem na żywo
  - `StartDiagnosticButton`: Przycisk rozpoczynający diagnostykę z LoadingButtonComponent
  - `AuthenticationStatus`: Informacja o stanie logowania z linkami do logowania/rejestracji
- **UX/Dostępność/Bezpieczeństwo**: 
  - Real-time validation z ARIA live regions dla licznika znaków
  - Semantic HTML z proper labeling
  - Brak dodatkowego potwierdzenia przed przejściem do diagnostyki
  - Sanityzacja inputu przed wysłaniem do API

### DiagnosticComponent (/diagnostic)
- **Główny cel**: Przeprowadzenie 3-fazowej diagnozy TCM z 15 pytaniami
- **Kluczowe informacje**: Aktualny progress, faza diagnostyki, pytanie, opcje odpowiedzi
- **Kluczowe komponenty**:
  - `DiagnosticProgressBarComponent`: Progress bar z formatem "Pytanie {current}/{total} - Faza {phase}"
  - `DiagnosticCardComponent`: Pojedyncza Card z dynamiczną zawartością pytań
  - `PhaseIndicatorComponent`: Opis aktualnej fazy diagnostycznej
  - `YesNoButtonsComponent`: Przyciski Tak/Nie z disabled state podczas API calls
- **UX/Dostępność/Bezpieczeństwo**:
  - ARIA live regions dla aktualizacji progress
  - Keyboard navigation (Tab/Enter)
  - Focus management przy zmianie pytań
  - Session timeout handling z Toast notifications
  - Disabled state zapobiega wielokrotnym kliknięciom

### RecommendationsComponent (/recommendations)
- **Główny cel**: Prezentacja wyników diagnozy i opcja zapisu rekomendacji
- **Kluczowe informacje**: Rekomendacje dietetyczne, plan posiłków, syndrom TCM
- **Kluczowe komponenty**:
  - `RecommendationDisplayComponent`: Scrollowalny kontener z rekomendacjami (domyślne zachowanie)
  - `SaveRecommendationButton`: Przycisk zapisu (tylko dla zalogowanych)
  - `ReturnToDiagnosticButton`: Przycisk powrotu do nowej diagnozy
  - `TCMSyndromeDisplay`: Sekcja z zidentyfikowanym syndromem
- **UX/Dostępność/Bezpieczeństwo**:
  - Semantic markup dla długich tekstów rekomendacji
  - AuthorizeView dla warunkowego wyświetlania przycisku zapisu
  - Proper heading hierarchy (h1-h6)
  - Print-friendly styling dla rekomendacji

### HistoryComponent (/history)
- **Główny cel**: Przeglądanie historii zapisanych rekomendacji
- **Kluczowe informacje**: Lista rekomendacji z datami, paginacja, szczegóły
- **Kluczowe komponenty**:
  - `HistoryTableComponent`: Bootstrap Table z paginacją (10 rekordów/stronę)
  - `PaginationComponent`: Kontrolki paginacji z nawigacją
  - `RecommendationPreviewComponent`: Modal/ekspandowane szczegóły rekomendacji
- **UX/Dostępność/Bezpieczeństwo**:
  - Horizontal scroll na urządzeniach mobilnych
  - ARIA pagination labels
  - Dostępny tylko dla zalogowanych użytkowników
  - RLS automatycznie filtruje dane użytkownika

### AuthenticationPages (/login, /register)
- **Główny cel**: Rejestracja i logowanie użytkowników
- **Kluczowe informacje**: Formularze uwierzytelniania, walidacja, błędy
- **Kluczowe komponenty**:
  - `LoginFormComponent`: Email, hasło, przycisk logowania
  - `RegisterFormComponent`: Email, hasło, potwierdzenie hasła
  - `ValidationSummaryComponent`: Wyświetlanie błędów walidacji
- **UX/Dostępność/Bezpieczeństwo**:
  - Strong password validation
  - ARIA error announcements
  - Secure token handling
  - CSRF protection

## 3. Mapa podróży użytkownika

### Główny przepływ diagnostyczny:
1. **Start** → Home (/) - wprowadzenie objawów lub pusty formularz
2. **Diagnoza** → Diagnostic (/diagnostic) - 3 fazy po 5 pytań każda:
   - Faza 1: Podstawowe wzorce energetyczne (pytania 1-5)
   - Faza 2: Szczegółowe objawy systemu (pytania 6-10)  
   - Faza 3: Finalizacja syndromu TCM (pytania 11-15)
3. **Wyniki** → Recommendations (/recommendations) - prezentacja rekomendacji
4. **Opcjonalny zapis** → History (/history) - dla zalogowanych użytkowników

### Przepływy pomocnicze:
- **Logowanie**: Home → Login → powrót do poprzedniej strony
- **Historia**: Navigation → History (bezpośredni dostęp dla zalogowanych)
- **Nowa diagnoza**: Recommendations → Home (przycisk "Powrót do diagnostyki")

### Obsługa błędów:
- **Wygasła sesja**: Diagnostic → Toast notification → Home
- **Błędy API**: Toast notifications z opcją powtórzenia
- **Błędy autoryzacji**: Przekierowanie do Login

## 4. Układ i struktura nawigacji

### Top Navigation Bar:
- **Logo/Brand**: QiBalance (link do Home)
- **Główne linki**: 
  - "Nowa Diagnoza" (zawsze widoczny)
  - "Historia Porad" (tylko dla zalogowanych - `AuthorizeView`)
- **Sekcja autoryzacji** (prawy górny róg):
  - Dla niezalogowanych: "Zaloguj się" / "Zarejestruj się"
  - Dla zalogowanych: "{user email}" / "Wyloguj się"

### Nawigacyjne decyzje:
- **Brak breadcrumb navigation** - upraszcza UX dla sekwencyjnego flow
- **Conditional navigation** - elementy ukrywane w zależności od stanu uwierzytelnienia
- **Mobile-first responsive** - hamburger menu na małych ekranach

### State management w nawigacji:
- `UserContext` jako Cascading Parameter dla całej aplikacji
- Real-time update stanu uwierzytelnienia
- Automatic redirect handling dla protected routes

## 5. Kluczowe komponenty

### Komponenty infrastrukturalne:
- **`MainLayoutComponent`**: Error Boundary, UserContext, Navigation wrapper
- **`ToastNotificationService`**: Globalne notyfikacje z domyślnymi stylami Bootstrap
- **`LoadingButtonComponent`**: Przyciski z disabled state i loading indicators
- **`SessionExpirationHandler`**: Automatyczna obsługa wygasłych sesji

### Komponenty diagnostyczne:
- **`DiagnosticProgressBarComponent`**: Wizualny postęp z szczegółowym formatem faz
- **`DiagnosticCardComponent`**: Reusable container dla pytań z dynamic content
- **`PhaseIndicatorComponent`**: Kontekstowe opisy faz diagnostycznych

### Komponenty rekomendacji:
- **`RecommendationDisplayComponent`**: Formatowanie i prezentacja długich tekstów
- **`SyndromeDisplayComponent`**: Highlightowanie zidentyfikowanego syndromu TCM
- **`SaveRecommendationComponent`**: Autoryzowana funkcjonalność zapisu

### Komponenty historii:
- **`HistoryTableComponent`**: Responsive tabela z paginacją
- **`PaginationComponent`**: Reusable pagination controls
- **`RecommendationPreviewModal`**: Quick view szczegółów

### Komponenty walidacji i form:
- **`SymptomsInputComponent`**: TextArea z real-time character counting
- **`FormValidationComponent`**: Centralized validation feedback
- **`AuthenticationFormComponent`**: Login/Register forms z secure handling

### Komponenty dostępności:
- **`AriaLiveRegionComponent`**: Dynamic content announcements
- **`FocusManagementComponent`**: Programatic focus handling
- **`SemanticMarkupWrapper`**: Proper heading hierarchy enforcement 