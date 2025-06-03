# Specyfikacja modułu autentykacji - Rejestracja, Logowanie oraz Odzyskiwanie Hasła

## 1. ARCHITEKTURA INTERFEJSU UŻYTKOWNIKA

### 1.1. Strony i Komponenty
- **Strona Rejestracji**: Formularz zawierający pola:
  - Adres e-mail
  - Hasło
  - Potwierdzenie hasła
  - Przycisk "Zarejestruj się"

- **Strona Logowania**: Formularz zawierający pola:
  - Adres e-mail
  - Hasło
  - Przycisk "Zaloguj się"

- **Strona Odzyskiwania Hasła**: Formularz zawierający pole:
  - Adres e-mail
  - Przycisk inicjujący wysłanie linku resetującego
  - *Uwaga: Funkcjonalność odzyskiwania hasła zostanie wdrożona w późniejszym etapie, zgodnie z wymaganiami PRD.*

### 1.2. Zmiany w Layoutach i Nawigacji
- Rozdzielenie trybu autoryzowanego (auth) i nieautoryzowanego (non-auth) na poziomie layoutu:
  - Tryb non-auth: dostęp do stron logowania, rejestracji oraz (planowanego) odzyskiwania hasła.
  - Tryb auth: rozszerzony layout z paskiem nawigacyjnym, przyciskiem wylogowania oraz dostępem do profilu i historii zaleceń.
- Dynamiczne przełączanie widoków w zależności od statusu sesji użytkownika.
- Dodatkowo, przyciski logowania oraz wylogowania umieszczone są w prawym górnym rogu, co odpowiada wymaganiom US-005.

### 1.3. Integracja z Backend
- Formularze będą integrowane ze stronami server-side Blazor, wysyłając dane do odpowiednich endpointów API.
- Implementacja walidacji po stronie klienta:
  - Sprawdzanie formatu adresu e-mail
  - Walidacja zgodności hasła z potwierdzeniem
  - Wyświetlanie komunikatów o błędach (np. "Nieprawidłowy adres e-mail", "Hasła nie są zgodne", "Błędne dane logowania")
- Obsługa stanów przejściowych (np. loader podczas oczekiwania na odpowiedź z backendu)

### 1.4. Scenariusze Użytkownika
- Użytkownik wypełnia formularz rejestracyjny i po poprawnej walidacji przesyłane są dane do backendu, otrzymując potwierdzenie rejestracji (np. e-mail weryfikacyjny).
- Po poprawnym logowaniu użytkownik widzi interfejs autoryzowany z dodatkowymi funkcjami, takimi jak dostęp do profilu i historii zaleceń.
- Użytkownik korzysta z formularza odzyskiwania hasła; funkcjonalność ta zostanie w pełni wdrożona w późniejszym etapie.
- Obsługa błędów sieciowych, walidacyjnych oraz wyjątków komunikowanych dynamicznie w interfejsie.

## 2. LOGIKA BACKENDOWA

### 2.1. Struktura Endpointów API
- **POST /api/auth/register**
  - Odbiera dane rejestracyjne (email, hasło, potwierdzenie hasła) i komunikuje się z Supabase Auth w celu utworzenia nowego konta.

- **POST /api/auth/login**
  - Odbiera dane logowania (email, hasło) i weryfikuje je, korzystając z mechanizmu Supabase Auth.

- **POST /api/auth/logout**
  - Kończy sesję, usuwając token autoryzacyjny użytkownika.

- **POST /api/auth/recover**
  - Inicjuje proces odzyskiwania hasła poprzez wysłanie żądania do Supabase Auth, który generuje link resetujący.
  - *Uwaga: Pełna integracja funkcjonalności odzyskiwania hasła zostanie zakończona w późniejszym etapie.*

- **GET /api/auth/refresh** (opcjonalnie)
  - Odświeża tokeny autoryzacyjne dla przedłużenia sesji.

### 2.2. Modele Danych
- `RegistrationRequest`: model danych rejestracyjnych (email, password, confirmPassword).
- `LoginRequest`: model danych logowania (email, password).
- `PasswordRecoveryRequest`: model zawierający email do odzyskiwania hasła.
- Standardowy model odpowiedzi API z informacjami o statusie operacji i ewentualnymi komunikatami błędów.

### 2.3. Walidacja i Obsługa Błędów
- Walidacja danych wejściowych na poziomie serwera za pomocą DataAnnotations w .NET.
- Middleware globalny do obsługi wyjątków, który przekształca wyjątki na standardowe odpowiedzi JSON z komunikatami błędów.
- Logowanie błędów przy użyciu mechanizmów loggingu dostępnych w .NET.

### 2.4. Renderowanie Stron w Server-Side Blazor
- Dynamiczne renderowanie stron na podstawie statusu autoryzacji użytkownika.
- Mechanizm przekierowywania do strony logowania w przypadku braku autoryzacji.
- Warunkowe renderowanie komponentów interfejsu w zależności od uprawnień użytkownika.

## 3. SYSTEM AUTENTYKACJI

### 3.1. Integracja z Supabase Auth
- Wykorzystanie Supabase Auth jako głównego systemu zarządzania autentykacją:
  - **Rejestracja**: Po przesłaniu formularza rejestracji dane są weryfikowane i przesyłane do Supabase w celu utworzenia konta. Otrzymywany jest token autoryzacyjny lub komunikat o błędzie.
  - **Logowanie**: Weryfikacja danych logowania (email, hasło) przy użyciu Supabase Auth oraz zwracanie tokenów autoryzacyjnych.
  - **Wylogowanie**: Usuwanie tokenu sesyjnego oraz zakończenie sesji użytkownika.
  - **Odzyskiwanie hasła**: Inicjowanie procesu resetowania hasła poprzez wysłanie żądania do Supabase Auth, który wysyła e-mail z linkiem resetującym.
    - *Uwaga: Mechanizm pełnego odzyskiwania hasła zostanie wdrożony w późniejszym etapie.*

### 3.2. Kluczowe Komponenty i Interfejsy
- Interfejs `IAuthService`: definiuje metody Register, Login, Logout oraz RecoverPassword.
- Klasa `AuthService`: implementuje `IAuthService` i korzysta z SDK lub REST API Supabase do komunikacji z systemem autentykacji.
- Kontrakty komunikacyjne między frontem a backendem oparte na żądaniach HTTP i modelach danych.

### 3.3. Bezpieczeństwo i Zarządzanie Sesją
- Wszystkie operacje autentykacji odbywają się przez połączenie HTTPS.
- Tokeny autoryzacyjne przechowywane są w bezpiecznych ciasteczkach (HttpOnly, Secure).
- Implementacja mechanizmu ograniczenia nieudanych prób logowania (np. blokada konta po kilku nieudanych próbach).
- Middleware odpowiedzialny za automatyczne odświeżanie sesji użytkownika.

## 4. PODSUMOWANIE

- Architektura modułu autentykacji jest zaprojektowana jako integralna część systemu QiBalance, z zachowaniem zgodności z istniejącymi funkcjonalnościami aplikacji.
- Interfejs użytkownika dynamicznie reaguje na status autoryzacji użytkownika, umożliwiając przejście między trybem auth i non-auth.
- Backend integruje się ściśle z Supabase Auth, realizując operacje rejestracji, logowania, wylogowania i odzyskiwania hasła zgodnie ze standardami bezpieczeństwa.
- Rozwiązanie jest modularne, co ułatwia rozbudowę, utrzymanie oraz zapewnia wysoki poziom bezpieczeństwa i niezawodności całego systemu.
- *Dodatkowo: Funkcjonalność odzyskiwania hasła oraz pełna integracja przycisków logowania/wylogowania w interfejsie zostaną uzupełnione zgodnie z wymaganiami PRD w kolejnych iteracjach.* 