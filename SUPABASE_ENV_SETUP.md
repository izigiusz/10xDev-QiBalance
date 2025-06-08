# Konfiguracja zmiennych środowiskowych dla Supabase

## Wymagane zmienne środowiskowe

Aplikacja QiBalance wymaga następujących zmiennych środowiskowych:

- `SUPABASE_URL` - URL Twojego projektu Supabase
- `SUPABASE_ANON_KEY` - Klucz publiczny (anon key) z Supabase
- `SUPABASE_JWT_SECRET` - Sekret JWT (opcjonalnie, dla dodatkowej walidacji)

## Konfiguracja lokalna (Development)

### Opcja 1: PowerShell (Windows)
```powershell
$env:SUPABASE_URL="https://your-project-ref.supabase.co"
$env:SUPABASE_ANON_KEY="your-public-anon-key"
$env:SUPABASE_JWT_SECRET="your-jwt-secret"
```

### Opcja 2: Command Prompt (Windows)
```cmd
set SUPABASE_URL=https://your-project-ref.supabase.co
set SUPABASE_ANON_KEY=your-public-anon-key
set SUPABASE_JWT_SECRET=your-jwt-secret
```

### Opcja 3: Plik .env (w katalogu głównym projektu)
```env
SUPABASE_URL=https://your-project-ref.supabase.co
SUPABASE_ANON_KEY=your-public-anon-key
SUPABASE_JWT_SECRET=your-jwt-secret
```

### Opcja 4: Visual Studio / Visual Studio Code
W `Properties/launchSettings.json`:
```json
{
  "profiles": {
    "QiBalance": {
      "commandName": "Project",
      "environmentVariables": {
        "SUPABASE_URL": "https://your-project-ref.supabase.co",
        "SUPABASE_ANON_KEY": "your-public-anon-key",
        "SUPABASE_JWT_SECRET": "your-jwt-secret"
      }
    }
  }
}
```

## Konfiguracja produkcyjna

### Azure App Service
1. Przejdź do Azure Portal → App Service → Configuration
2. Dodaj Application Settings:
   - `SUPABASE_URL`
   - `SUPABASE_ANON_KEY`
   - `SUPABASE_JWT_SECRET`

### AWS Elastic Beanstalk
1. W konsoli AWS → Elastic Beanstalk → Configuration → Software
2. Dodaj Environment Properties

### Docker
```dockerfile
ENV SUPABASE_URL=https://your-project-ref.supabase.co
ENV SUPABASE_ANON_KEY=your-public-anon-key
ENV SUPABASE_JWT_SECRET=your-jwt-secret
```

Lub użyj pliku docker-compose:
```yaml
version: '3.8'
services:
  qibalance:
    build: .
    environment:
      - SUPABASE_URL=https://your-project-ref.supabase.co
      - SUPABASE_ANON_KEY=your-public-anon-key
      - SUPABASE_JWT_SECRET=your-jwt-secret
```

## Gdzie znaleźć klucze Supabase

1. Zaloguj się do [Supabase Dashboard](https://supabase.com/dashboard)
2. Wybierz swój projekt
3. Przejdź do Settings → API
4. Skopiuj:
   - `URL` → użyj jako `SUPABASE_URL`
   - `anon public` → użyj jako `SUPABASE_ANON_KEY`
   - `service_role secret` → użyj jako `SUPABASE_JWT_SECRET` (tylko jeśli potrzebne)

## Bezpieczeństwo

⚠️ **WAŻNE**:
- Nigdy nie commituj kluczy do repozytorium Git
- Dodaj `.env` do `.gitignore`
- Używaj różnych kluczy dla development i production
- `anon key` jest bezpieczny do użycia po stronie klienta
- `service_role key` powinien być używany tylko po stronie serwera

## Testowanie konfiguracji

Po skonfigurowaniu zmiennych, uruchom aplikację:
```bash
dotnet run
```

Aplikacja powinna się uruchomić bez błędów związanych z konfiguracją Supabase. 