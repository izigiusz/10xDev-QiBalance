## Schemat bazy danych dla QiBalance

### 1. Tabele

#### users
This table is managed by Supabase Auth.

- **email**: VARCHAR NOT NULL PRIMARY KEY UNIQUE
- **password**: VARCHAR NOT NULL

#### recommendations
- **recommendation_id**: UUID PRIMARY KEY
- **user_id**: VARCHAR NOT NULL
  - Klucz obcy odwołujący się do `users(email)`
- **date_generated**: TIMESTAMP WITH TIME ZONE NOT NULL
- **recommendation_text**: TEXT NOT NULL

### 2. Relacje

- Relacja jeden-do-wielu między tabelą `users` a `recommendations`:
  - Każdy użytkownik (zidentyfikowany przez unikalny `email`) może posiadać wiele rekomendacji, co jest realizowane poprzez kolumnę `user_id` w tabeli `recommendations` odwołującą się do `users(email)`

### 3. Indeksy

- Indeks na kluczach podstawowych:
  - `users(email)`
  - `recommendations(recommendation_id)`
- Dodatkowy indeks na kolumnie `user_id` w tabeli `recommendations` dla optymalizacji zapytań filtrujących po użytkowniku

### 4. Zasady PostgreSQL – Row-Level Security (RLS)

W tabeli `recommendations` wdrożone zostaną polityki RLS oparte na kolumnie `user_id`, aby zapewnić, że użytkownik ma dostęp tylko do swoich danych. Przykładowa konfiguracja może wyglądać następująco:

```sql
ALTER TABLE recommendations ENABLE ROW LEVEL SECURITY;

CREATE POLICY recommendations_select_policy ON recommendations
  FOR SELECT
  USING (user_id = current_setting('app.current_user_email'));

CREATE POLICY recommendations_modify_policy ON recommendations
  FOR UPDATE, DELETE
  USING (user_id = current_setting('app.current_user_email'));
```

*Uwagi:* 
- W powyższych politykach zakłada się, że aplikacja ustawia zmienną konfiguracyjną `app.current_user_email` identyfikującą bieżącego użytkownika.
- W praktyce, przed wdrożeniem polityk RLS, należy zadbać o odpowiednią konfigurację aplikacji oraz rozszerzeń PostgreSQL umożliwiających generowanie identyfikatorów UUID (np. `pgcrypto` lub `uuid-ossp`).

### 5. Dodatkowe uwagi

- Zastosowano standardowe typy danych PostgreSQL: VARCHAR, UUID, TIMESTAMP WITH TIME ZONE, TEXT.
- Ograniczenia NOT NULL oraz UNIQUE (dla `users(email)`) gwarantują integralność danych.
- Schemat jest zoptymalizowany pod kątem MVP, eliminując zbędne tabele (np. brak przechowywania odpowiedzi diagnostycznych) i skupiając się na kluczowych funkcjonalnościach: zarządzaniu użytkownikami oraz przechowywaniu generowanych rekomendacji. 