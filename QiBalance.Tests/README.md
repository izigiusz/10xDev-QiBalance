# QiBalance.Tests - Testy Jednostkowe

## Przegląd

Ten projekt zawiera kompleksowe testy jednostkowe dla aplikacji QiBalance, skupiające się na testowaniu krytycznych komponentów biznesowych, szczególnie **ValidationService**.

## Dlaczego ValidationService.ValidateUserId()?

**ValidationService.ValidateUserId()** została wybrana jako najbardziej krytyczna metoda do testowania z następujących powodów:

### 🔒 Bezpieczeństwo aplikacji
- **Walidacja identyfikatorów użytkowników** - Chroni przed atakami z użyciem nieprawidłowych ID
- **Podwójna walidacja** - Obsługuje zarówno GUID (Supabase) jak i adresy email
- **Centralna rola** - Używana przez wszystkie serwisy w aplikacji

### 🎯 Idealna dla testów jednostkowych
- **Czysta funkcja** - Brak zależności zewnętrznych
- **Jasne przypadki testowe** - Łatwe do zdefiniowania scenariusze
- **Deterministyczne wyniki** - Zawsze te same wyniki dla tych samych danych

## Pokrycie testów

### Pozytywne scenariusze ✅
- Prawidłowe identyfikatory GUID
- Prawidłowe adresy email
- Przypadki graniczne (empty GUID, długie emaile)
- Złożone prawidłowe emaile

### Negatywne scenariusze ❌
- Wartości null i puste
- Nieprawidłowe formaty email
- Nieprawidłowe GUID
- Próby ataków (XSS, SQL injection, path traversal)

### Testowanie logowania 📝
- Weryfikacja, że ostrzeżenia są zapisywane dla nieprawidłowych danych
- Sprawdzenie, że prawidłowe dane nie generują ostrzeżeń

## Uruchamianie testów

### Lokalnie

```bash
# Uruchomienie wszystkich testów
dotnet test

# Uruchomienie z szczegółowymi informacjami
dotnet test --verbosity normal

# Uruchomienie z pokryciem kodu
dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults
```

### W GitHub Actions

Testy automatycznie uruchamiają się przy każdym:
- **Push** do repozytorium
- **Pull Request**

#### Workflow `main.yml`
1. **Test Job** - Uruchamia testy jednostkowe
2. **Build and Deploy Job** - Buduje i wdraża aplikację (tylko dla main/master)

#### Workflow `test-coverage.yml`
- Generuje raporty pokrycia kodu
- Przesyła artefakty do GitHub
- Integruje się z Codecov

## Technologie

- **xUnit** - Framework testowy
- **Moq** - Mock objects
- **FluentAssertions** - Asercje w naturalnym języku
- **Coverlet** - Pokrycie kodu

## Struktura testów

```
ValidationServiceTests
├── ValidateUserId Tests
│   ├── Positive scenarios (valid GUIDs, emails)
│   ├── Negative scenarios (invalid formats)
│   └── Logging verification
├── Edge Cases and Security Tests
│   ├── Boundary testing
│   └── Security attack simulation
└── Performance Tests
    └── Multiple validation consistency
```

## Statystyki

- **34 testów** - Komprehensywne pokrycie
- **100% sukces** - Wszystkie testy przechodzą
- **0 flakey tests** - Deterministyczne wyniki

## Korzyści dla projektu

### 🛡️ Zwiększone bezpieczeństwo
- Wczesne wykrywanie luk bezpieczeństwa
- Ochrona przed atakami przez walidację

### 🚀 Ciągła integracja
- Automatyczne testowanie przy każdej zmianie
- Blokada wdrożeń z błędami
- Raporty pokrycia kodu

### 🔧 Ułatwiona refaktoryzacja
- Bezpieczne zmiany w kodzie
- Natychmiastowe feedback o regresji
- Dokumentacja oczekiwanego zachowania

## Przyszłe rozszerzenia

1. **Więcej serwisów** - DiagnosticService, RecommendationService
2. **Testy integracyjne** - Testowanie z prawdziwą bazą danych
3. **Performance testing** - Testowanie wydajności
4. **E2E testing** - Testowanie całego przepływu użytkownika

---

*Ten test jednostkowy stanowi fundament dla bezpiecznej i niezawodnej aplikacji QiBalance.* 