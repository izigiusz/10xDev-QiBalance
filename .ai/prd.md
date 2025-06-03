# Dokument wymagań produktu (PRD) - QiBalance
## 1. Przegląd produktu
QiBalance to aplikacja webowa w wersji MVP, stworzona w celu wspierania użytkowników w osiąganiu równowagi przepływu Qi w organizmie poprzez odpowiednie odżywianie, zgodnie z zasadami Tradycyjnej Medycyny Chińskiej. System generuje spersonalizowane zalecenia dietetyczne wraz z tygodniowym planem posiłków (śniadanie, obiad, kolacja), opierając się na dostarczonych informacjach o stanie zdrowia lub poprzez automatyczne włączenie trybu diagnostycznego.

## 2. Problem użytkownika
Użytkownik często nie jest świadomy drobnych nieprawidłowości w swoim stanie zdrowia, które mogą wpływać na równowagę przepływu Qi. Brak precyzyjnych informacji opiera się głównie na subiektywnych odczuciach, co utrudnia wybór właściwych metod poprawy zdrowia. Aplikacja ma za zadanie:
- Zidentyfikować syndrom w/g TMCH na podstawie ręcznie wprowadzonych objawów (opcjonalnie) i poprzez tryb diagnostyczny.
- Zapewnić spersonalizowane zalecenia dietetyczne w oparciu o dynamicznie generowane pytania diagnostyczne.

## 3. Wymagania funkcjonalne
1. Mechanizm wprowadzania objawów medycznych:
   - Pole tekstowe umożliwiające wpisanie aktualnych objawów (np. ból głowy, chudość).
   - Akceptacja pustego pola, skutkująca automatycznym uruchomieniem trybu diagnostycznego.

2. Dynamiczne generowanie pytań diagnostycznych:
   - System generuje adaptacyjne pytania diagnostyczne według zasad Tradycyjnej Medycyny Chińskiej.
   - Pytania są prezentowane jako opcje typu Tak/Nie z checkboxami.
   - Użytkownik odpowiada na pytania, a odpowiedzi zbierane są w celu dalszej analizy.
   - Ma być wygenerowanych 15 pytań.

3. Prezentacja i zapis zaleceń:
   - Generowanie spersonalizowanych zaleceń dietetycznych oraz tygodniowego planu posiłków.
   - Przycisk "Zapisz" umożliwiający zapisanie otrzymanych zaleceń do trwałej historii.
   - Historia zaleceń jest dostępna w profilu użytkownika i nie może być edytowana.

4. System autoryzacji i zarządzanie kontami:
   - Rejestracja i logowanie użytkowników dla bezpiecznego dostępu.
   - Szczegóły implementacji autoryzacji ustalone będą w kolejnych etapach.

5. Interfejs użytkownika:
   - Sekwencyjny przepływ działań: wprowadzenie objawów → wysłanie formularza → generowanie pytań diagnostycznych → prezentacja zaleceń → zapis wyników.
   - Aplikacja jest zoptymalizowana pod kątem przeglądarek internetowych.

## 4. Granice produktu
- Importowanie przepisów z adresów URL.
- Rozbudowana obsługa multimediów (np. zdjęcia przepisów).
- Udostępnianie zaleceń innym użytkownikom.
- Funkcje społecznościowe (np. komentowanie, lajkowanie).

## 5. Historyjki użytkowników

US-001  
Tytuł: Rejestracja i logowanie użytkownika  
Opis: Użytkownik rejestruje się lub loguje do systemu za pomocą formularza uwierzytelniania, uzyskując dostęp do swojego profilu i historii zapisanych zaleceń.  
Kryteria akceptacji:  
- Użytkownik może utworzyć konto lub zalogować się do istniejącego.  
- Po poprawnej autoryzacji system przyznaje dostęp do profilu i zapisanej historii.

US-002  
Tytuł: Wprowadzanie objawów medycznych  
Opis: Użytkownik wprowadza informacje o aktualnych objawach medycznych w dedykowanym polu tekstowym. W przypadku braku danych system automatycznie uruchamia domyślny tryb diagnostyczny.  
Kryteria akceptacji:  
- Pole tekstowe umożliwia wpisanie dowolnych objawów.  
- W przypadku pustego pola system przełącza się na domyślny tryb diagnostyczny.

US-003  
Tytuł: Generowanie dynamicznych pytań diagnostycznych
Opis: Na podstawie wprowadzonych objawów lub braku danych, system generuje adaptacyjne pytania diagnostyczne (Tak/Nie z checkboxami) według zasad Tradycyjnej Medycyny Chińskiej, w celu zebrania dodatkowych informacji.  
Kryteria akceptacji:  
- System generuje pytania dostosowane do stanu zdrowia użytkownika.  
- Pytania są prezentowane, a odpowiedzi użytkownika są poprawnie rejestrowane.

US-004  
Tytuł: Prezentacja i zapis zaleceń dietetycznych  
Opis: Po udzieleniu odpowiedzi na pytania diagnostyczne, system generuje spersonalizowane zalecenia dietetyczne oraz plan posiłków. Użytkownik zapisuje wyniki, które są przechowywane w nieedytowalnej historii.  
Kryteria akceptacji:  
- Użytkownik otrzymuje zestaw zaleceń i plan posiłków po wypełnieniu pytań diagnostycznych.  
- Po kliknięciu przycisku „Zapisz” zalecenia są trwale przechowywane w profilu.  
- Zapisane zalecenia nie mogą być modyfikowane.


US-005: Bezpieczny dostęp i uwierzytelnianie

- Tytuł: Bezpieczny dostęp
- Opis: Jako użytkownik chcę mieć możliwość rejestracji i logowania się do systemu w sposób zapewniający bezpieczeństwo moich danych.
- Kryteria akceptacji:
  - Logowanie i rejestracja odbywają się na dedykowanych stronach.
  - Logowanie wymaga podania adresu email i hasła.
  - Rejestracja wymaga podania adresu email, hasła i potwierdzenia hasła.
  - Użytkownik MOŻE korzystać z tworzenia zaleceń dietetycznych "ad-hoc" bez logowania się do systemu (US-001).
  - Użytkownik NIE MOŻE zapisywać usyskanych zaleceń bez logowania się do systemu (US-003).
  - Użytkownik może logować się do systemu poprzez przycisk w prawym górnym rogu.
  - Użytkownik może się wylogować z systemu poprzez przycisk w prawym górnym rogu.
  - Nie korzystamy z zewnętrznych serwisów logowania (np. Google, GitHub).
  - Odzyskiwanie hasła powinno w późniejszym etapie

## 6. Metryki sukcesu
- 75% użytkowników generuje przynajmniej jedno zalecenie dietetyczne raz na pół roku.  
- Monitorowanie liczby zapisanych zaleceń oraz aktywności użytkowników w trakcie sesji diagnostycznych.