# Konfiguracja AI Services dla QiBalance

## Wymagane Pakiety NuGet

Aby aktywować serwisy AI z podejściem 3-fazowym, zainstaluj następujące pakiety:

```bash
dotnet add package Microsoft.SemanticKernel
dotnet add package Microsoft.SemanticKernel.Plugins.OpenAI
```

## Konfiguracja Environment Variables

Dodaj do Environment Variables:

```
OPENAI_API_KEY=your_openai_api_key_here
```

## Aktywacja Serwisów AI

Po zainstalowaniu pakietów, odkomentuj w `Program.cs` sekcję AI Services:

```csharp
// AI Services - Semantic Kernel Configuration
var openAIKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrEmpty(openAIKey))
{
    throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set");
}

builder.Services.AddKernel()
    .AddOpenAIChatCompletion("gpt-4", openAIKey);

builder.Services.AddScoped<IOpenAIService, OpenAIService>();
builder.Services.AddScoped<IDiagnosticService, DiagnosticService>();
```

## Korzyści Systemu 3-Fazowego

- **80% redukcja kosztów AI**: Tylko 3 wywołania zamiast 15
- **Lepsza personalizacja**: Pytania adaptują się progresywnie
- **Optymalna diagnostyka TCM**: Logiczna progresja od ogólnych do specjalistycznych
- **Skalowalność**: Łatwe dodawanie nowych faz

## Testowanie

Aby przetestować działanie systemu bez OpenAI:
1. Można utworzyć mock implementację `IOpenAIService`
2. Zwracać statyczne pytania i rekomendacje
3. Testować logikę przepływu 3-fazowego

## Health Checks (Opcjonalne)

Dodaj health check dla OpenAI service:

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<OpenAIHealthCheck>("openai");
``` 