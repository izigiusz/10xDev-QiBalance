using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using QiBalance.Models.DTOs;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace QiBalance.Services
{
    /// <summary>
    /// Interface for OpenAI service with 3-phase diagnostic approach
    /// Provides AI-powered question generation and recommendation creation for TCM diagnosis
    /// </summary>
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

    /// <summary>
    /// Service providing AI-powered diagnostic functionality using Semantic Kernel and OpenAI
    /// Implements 3-phase approach for 80% cost reduction while maintaining quality
    /// </summary>
    public class OpenAIService : IOpenAIService
    {
        private readonly Kernel _kernel;
        private readonly ILogger<OpenAIService> _logger;
        private readonly IValidationService _validationService;
        private readonly IChatCompletionService _chatCompletionService;

        // JSON serialization options for consistent parsing
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public OpenAIService(Kernel kernel, ILogger<OpenAIService> logger, IValidationService validationService)
        {
            _kernel = kernel;
            _logger = logger;
            _validationService = validationService;
            _chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
        }

        /// <summary>
        /// Generate questions for specific diagnostic phase with progressive personalization
        /// Phase 1: Basic patterns (Qi, Yang/Yin, constitution)
        /// Phase 2: System specialization (organs, pathological patterns)  
        /// Phase 3: TCM syndrome finalization (differentiation, confirmation)
        /// </summary>
        public async Task<DiagnosticPhase> GeneratePhaseQuestionsAsync(
            int phase, 
            string? initialSymptoms, 
            List<DiagnosticAnswer> previousAnswers)
        {
            try
            {
                // Validate input parameters
                if (phase < 1 || phase > 3)
                    throw new ValidationException("Faza diagnostyczna musi być między 1 a 3");

                _validationService.ValidateSymptoms(initialSymptoms);

                _logger.LogInformation("Generating phase {Phase} questions with {AnswerCount} previous answers", 
                    phase, previousAnswers.Count);

                // Build prompt based on phase
                var prompt = BuildPhasePrompt(phase, initialSymptoms, previousAnswers);
                
                // Call OpenAI
                var response = await _chatCompletionService.GetChatMessageContentAsync(prompt);
                
                if (string.IsNullOrEmpty(response.Content))
                    throw new InvalidOperationException("OpenAI returned empty response");

                // Parse JSON response
                var diagnosticPhase = ParseDiagnosticPhase(response.Content, phase);

                _logger.LogInformation("Successfully generated {QuestionCount} questions for phase {Phase}", 
                    diagnosticPhase.Questions.Count, phase);

                return diagnosticPhase;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating phase {Phase} questions", phase);
                throw;
            }
        }

        /// <summary>
        /// Generate comprehensive TCM recommendations based on complete diagnostic session
        /// Creates detailed dietary document with weekly meal plan
        /// </summary>
        public async Task<RecommendationResult> GenerateRecommendationsAsync(
            string? initialSymptoms, 
            List<DiagnosticAnswer> diagnosticAnswers)
        {
            try
            {
                _validationService.ValidateSymptoms(initialSymptoms);

                if (diagnosticAnswers.Count != 15)
                    throw new ValidationException("Wymagane jest dokładnie 15 odpowiedzi diagnostycznych");

                _logger.LogInformation("Generating final recommendations based on {AnswerCount} answers", 
                    diagnosticAnswers.Count);

                // Build comprehensive recommendation prompt
                var prompt = BuildRecommendationPrompt(initialSymptoms, diagnosticAnswers);
                
                // Call OpenAI for final recommendations
                var response = await _chatCompletionService.GetChatMessageContentAsync(prompt);
                
                if (string.IsNullOrEmpty(response.Content))
                    throw new InvalidOperationException("OpenAI returned empty response for recommendations");

                // Parse JSON response
                var recommendations = ParseRecommendationResult(response.Content);

                _validationService.ValidateRecommendationText(recommendations.RecommendationText);

                _logger.LogInformation("Successfully generated recommendations for TCM syndrome: {Syndrome}", 
                    recommendations.TcmSyndrome);

                return recommendations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating recommendations");
                throw;
            }
        }

        /// <summary>
        /// Build phase-specific prompt for question generation
        /// Each phase focuses on different aspects of TCM diagnosis
        /// </summary>
        private static string BuildPhasePrompt(int phase, string? initialSymptoms, List<DiagnosticAnswer> previousAnswers)
        {
            var phaseInfo = GetPhaseInfo(phase);
            var symptomsText = string.IsNullOrEmpty(initialSymptoms) ? "Brak konkretnych objawów" : initialSymptoms;
            
            var prompt = $@"Jesteś ekspertem tradycyjnej medycyny chińskiej (TCM). Twoim celem jest zdiagnozowanie różnicowe syndromu według medycyny Chińskiej w celu przygotowania zaleceń dietetycznych. Osoba diagnozowana nie słyszała o Medycynie Chińskiej, uwzględnij to. Wygeneruj dokładnie 5 pytań typu tak/nie dla {phaseInfo.Name}.

Objawy początkowe: {symptomsText}

{BuildPreviousAnswersText(previousAnswers)}

{phaseInfo.Focus}

WAŻNE: Zwróć TYLKO poprawny JSON bez dodatkowych komentarzy:
{{
  ""phase"": {phase},
  ""phaseName"": ""{phaseInfo.Name}"",
  ""phaseDescription"": ""{phaseInfo.Description}"",
  ""questions"": [
    {{""id"": ""q{phase}_1"", ""questionText"": ""Twoje pierwsze pytanie?"", ""questionType"": 0}},
    {{""id"": ""q{phase}_2"", ""questionText"": ""Twoje drugie pytanie?"", ""questionType"": 0}},
    {{""id"": ""q{phase}_3"", ""questionText"": ""Twoje trzecie pytanie?"", ""questionType"": 0}},
    {{""id"": ""q{phase}_4"", ""questionText"": ""Twoje czwarte pytanie?"", ""questionType"": 0}},
    {{""id"": ""q{phase}_5"", ""questionText"": ""Twoje piąte pytanie?"", ""questionType"": 0}}
  ]
}}";

            return prompt;
        }

        /// <summary>
        /// Build comprehensive prompt for final recommendations
        /// </summary>
        private static string BuildRecommendationPrompt(string? initialSymptoms, List<DiagnosticAnswer> diagnosticAnswers)
        {
            var symptomsText = string.IsNullOrEmpty(initialSymptoms) ? "Brak konkretnych objawów" : initialSymptoms;
            var answersText = string.Join("\n", diagnosticAnswers.Select(a => $"- {a.Question}: {(a.Answer ? "Tak" : "Nie")}"));

            return $@"Jesteś ekspertem medycyny chińskiej (TCM). Na podstawie kompletnej diagnozy wygeneruj szczegółowe rekomendacje dietetyczne.

Objawy początkowe: {symptomsText}

Kompletne odpowiedzi diagnostyczne (15 pytań):
{answersText}

Na podstawie tej diagnozy:
1. Określ główny syndrom TCM
2. Wygeneruj szczegółowe rekomendacje dietetyczne (minimum 2000 słów)
3. Zaproponuj tygodniowy plan posiłków (7 dni)
4. Wskaż produkty do unikania
5. Dodaj porady dotyczące stylu życia zgodne z TCM

WAŻNE: Zwróć TYLKO poprawny JSON bez dodatkowych komentarzy:
{{
  ""recommendationText"": ""Tutaj umieść kompletny dokument z rekomendacjami dietetycznymi, planem posiłków na tydzień, produktami do unikania i poradami dotyczącymi stylu życia. Dokument powinien mieć minimum 2000 słów."",
  ""tcmSyndrome"": ""Nazwa zidentyfikowanego syndromu TCM""
}}";
        }

        /// <summary>
        /// Get phase-specific information for prompt building
        /// </summary>
        private static (string Name, string Description, string Focus) GetPhaseInfo(int phase)
        {
            return phase switch
            {
                1 => (
                    "Podstawowa ocena",
                    "Pytania dotyczące podstawowych objawów i stanu ogólnego",
                    "Skoncentruj się na podstawowych wzorcach energetycznych TCM:\n- Stan Qi (energia życiowa)\n- Balans Yang/Yin\n- Konstytucja ogólna\n- Podstawowe funkcje narządów\n- Ogólny stan zdrowia"
                ),
                2 => (
                    "Pogłębiona analiza", 
                    "Pytania o szczegółowe objawy i wzorce energetyczne",
                    "Na podstawie odpowiedzi z fazy 1, skoncentruj się na:\n- Konkretnych systemach narządowych wymagających uwagi\n- Szczegółowych wzorcach Qi i Krwi\n- Specyficznych objawach patologicznych\n- Czynnikach zewnętrznych wpływających na zdrowie"
                ),
                3 => (
                    "Specjalistyczna diagnoza",
                    "Pytania kluczowe dla ostatecznej diagnozy TCM", 
                    "Na podstawie całości odpowiedzi, skoncentruj się na:\n- Ostatecznym określeniu syndromu TCM\n- Różnicowaniu między podobnymi wzorcami\n- Najważniejszych aspektach potwierdzających diagnozę\n- Kluczowych objawach decydujących o leczeniu"
                ),
                _ => throw new ArgumentException($"Invalid phase: {phase}")
            };
        }

        /// <summary>
        /// Build text representation of previous answers for context
        /// </summary>
        private static string BuildPreviousAnswersText(List<DiagnosticAnswer> previousAnswers)
        {
            if (previousAnswers.Count == 0)
                return "";

            var answersText = string.Join("\n", previousAnswers.Select(a => $"- {a.Question}: {(a.Answer ? "Tak" : "Nie")}"));
            return $"Odpowiedzi z poprzednich faz:\n{answersText}\n";
        }

        /// <summary>
        /// Parse DiagnosticPhase from OpenAI JSON response
        /// </summary>
        private DiagnosticPhase ParseDiagnosticPhase(string jsonResponse, int expectedPhase)
        {
            try
            {
                var cleanJson = ExtractJsonFromResponse(jsonResponse);
                var phase = JsonSerializer.Deserialize<DiagnosticPhase>(cleanJson, JsonOptions);
                
                if (phase == null)
                    throw new InvalidOperationException("Failed to deserialize DiagnosticPhase");

                // Validate response
                if (phase.Phase != expectedPhase)
                    throw new InvalidOperationException($"Expected phase {expectedPhase}, got {phase.Phase}");

                if (phase.Questions.Count != 5)
                    throw new InvalidOperationException($"Expected 5 questions, got {phase.Questions.Count}");

                return phase;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse DiagnosticPhase JSON: {JsonResponse}", jsonResponse);
                throw new InvalidOperationException("Invalid JSON response from OpenAI", ex);
            }
        }

        /// <summary>
        /// Parse RecommendationResult from OpenAI JSON response
        /// </summary>
        private RecommendationResult ParseRecommendationResult(string jsonResponse)
        {
            try
            {
                var cleanJson = ExtractJsonFromResponse(jsonResponse);
                var result = JsonSerializer.Deserialize<RecommendationResult>(cleanJson, JsonOptions);
                
                if (result == null)
                    throw new InvalidOperationException("Failed to deserialize RecommendationResult");

                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse RecommendationResult JSON: {JsonResponse}", jsonResponse);
                throw new InvalidOperationException("Invalid JSON response from OpenAI", ex);
            }
        }

        /// <summary>
        /// Extract clean JSON from OpenAI response (removes markdown formatting)
        /// </summary>
        private static string ExtractJsonFromResponse(string response)
        {
            var trimmed = response.Trim();
            
            // Remove markdown code blocks if present
            if (trimmed.StartsWith("```json"))
            {
                var startIndex = trimmed.IndexOf('{');
                var endIndex = trimmed.LastIndexOf('}');
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    return trimmed.Substring(startIndex, endIndex - startIndex + 1);
                }
            }
            
            // Try to find JSON object boundaries
            var jsonStart = trimmed.IndexOf('{');
            var jsonEnd = trimmed.LastIndexOf('}');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                return trimmed.Substring(jsonStart, jsonEnd - jsonStart + 1);
            }
            
            return trimmed;
        }
    }
} 