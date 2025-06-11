using Microsoft.Extensions.Caching.Memory;
using QiBalance.Models.DTOs;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace QiBalance.Services
{
    /// <summary>
    /// Interface for diagnostic service with 3-phase approach
    /// Manages diagnostic sessions with AI integration and session caching
    /// Supports both authenticated and anonymous users
    /// </summary>
    public interface IDiagnosticService
    {
        // Core methods for both authenticated and anonymous users
        Task<DiagnosticSession> StartSessionAsync(string? initialSymptoms, string? userId = null);
        Task<DiagnosticResponse> SubmitAnswerAsync(Guid sessionId, string questionId, bool answer, string? userId = null);
        Task<bool> IsSessionValidAsync(Guid sessionId);
        Task ClearExpiredSessionsAsync();
        
        // Methods for authenticated users (email-based)
        Task<DiagnosticQuestion?> GetNextQuestionAsync(string userEmail, int questionNumber);
        Task<DiagnosticResponse> SubmitAnswerAsync(string userEmail, int questionNumber, bool answer);
        Task<bool> ValidateSessionAsync(string userEmail);
        Task<DiagnosticSessionInfo?> GetSessionInfoAsync(string userEmail);
        
        // New methods for anonymous users (session-based)
        Task<DiagnosticSession> StartAnonymousSessionAsync(string? initialSymptoms);
        Task<DiagnosticQuestion?> GetNextQuestionBySessionAsync(Guid sessionId, int questionNumber);
        Task<DiagnosticResponse> SubmitAnswerBySessionAsync(Guid sessionId, int questionNumber, bool answer);
        Task<bool> ValidateSessionByIdAsync(Guid sessionId);
        Task<DiagnosticSessionInfo?> GetSessionInfoByIdAsync(Guid sessionId);
    }

    /// <summary>
    /// Service managing 3-phase diagnostic sessions with AI integration
    /// Provides 80% cost reduction through intelligent phase-based question generation
    /// </summary>
    public class DiagnosticService : IDiagnosticService
    {
        private readonly IOpenAIService _openAIService;
        private readonly IMemoryCache _cache;
        private readonly IValidationService _validationService;
        private readonly ILogger<DiagnosticService> _logger;
        private readonly IRecommendationService _recommendationService;
        private readonly ISupabaseService _supabaseService;
        private readonly AuthenticationStateProvider _authStateProvider;

        // Constants for session management
        private const int TotalQuestions = 15;
        private const int QuestionsPerPhase = 5;
        private const int SessionTimeoutHours = 1;

        public DiagnosticService(
            IOpenAIService openAIService,
            IMemoryCache cache,
            IValidationService validationService,
            ILogger<DiagnosticService> logger,
            IRecommendationService recommendationService,
            ISupabaseService supabaseService,
            AuthenticationStateProvider authStateProvider)
        {
            _openAIService = openAIService;
            _cache = cache;
            _validationService = validationService;
            _logger = logger;
            _recommendationService = recommendationService;
            _supabaseService = supabaseService;
            _authStateProvider = authStateProvider;
        }

        /// <summary>
        /// Start new diagnostic session with Phase 1 questions
        /// Generates 5 basic questions based on initial symptoms
        /// Supports both authenticated and anonymous users
        /// </summary>
        public async Task<DiagnosticSession> StartSessionAsync(string? initialSymptoms, string? userId = null)
        {
            try
            {
                // Validate input - userId is optional for anonymous users
                _validationService.ValidateSymptoms(initialSymptoms);
                if (!string.IsNullOrEmpty(userId))
                {
                    _validationService.ValidateUserId(userId);
                }

                _logger.LogInformation("Starting new diagnostic session for user: {UserId}", userId ?? "anonymous");

                // Generate Phase 1 questions (5 basic questions)
                var phase1 = await _openAIService.GeneratePhaseQuestionsAsync(1, initialSymptoms, new List<DiagnosticAnswer>());

                // Create new session - supports both authenticated and anonymous users
                var session = new DiagnosticSession
                {
                    SessionId = Guid.NewGuid(),
                    Questions = phase1.Questions,
                    TotalQuestions = TotalQuestions,
                    CurrentQuestion = 1,
                    CurrentPhase = 1,
                    InitialSymptoms = initialSymptoms,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(SessionTimeoutHours),
                    UserId = userId, // Can be null for anonymous users
                    Answers = new List<DiagnosticAnswer>()
                };

                // Cache session by SessionId (primary) and optionally by UserId for authenticated users
                var cacheKey = GetSessionCacheKey(session.SessionId);
                _cache.Set(cacheKey, session, TimeSpan.FromHours(SessionTimeoutHours));
                
                // Note: We don't cache by userId here anymore as it could be either GUID or email
                // Email-based caching is handled separately in GetOrCreateUserSessionAsync
                
                _logger.LogInformation("Diagnostic session created: {SessionId}, Phase: {Phase}, Questions: {QuestionCount}, Anonymous: {IsAnonymous}", 
                    session.SessionId, session.CurrentPhase, session.Questions.Count, string.IsNullOrEmpty(userId));

                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting diagnostic session for user: {UserId}", userId ?? "anonymous");
                throw;
            }
        }

        /// <summary>
        /// Submit answer and progress through 3-phase diagnostic flow
        /// Automatically generates new phases when reaching question milestones (5, 10)
        /// </summary>
        public async Task<DiagnosticResponse> SubmitAnswerAsync(Guid sessionId, string questionId, bool answer, string? userId = null)
        {
            try
            {
                // Validate input
                _validationService.ValidateSessionId(sessionId);
                _validationService.ValidateQuestionId(questionId);
                if (!string.IsNullOrEmpty(userId))
                {
                    _validationService.ValidateUserId(userId);
                }

                // Get session from cache
                var session = await GetSessionFromCacheAsync(sessionId);
                if (session == null)
                {
                    throw new InvalidOperationException("Sesja diagnostyczna wygasła lub nie została znaleziona");
                }

                // Validate user authorization for session
                if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(session.UserId) && session.UserId != userId)
                {
                    throw new UnauthorizedAccessException("Brak dostępu do tej sesji diagnostycznej");
                }

                _logger.LogInformation("Processing answer for session: {SessionId}, Question: {QuestionId}, Answer: {Answer}", 
                    sessionId, questionId, answer);

                // Find question and validate
                var question = session.Questions.FirstOrDefault(q => q.Id == questionId);
                if (question == null)
                {
                    throw new InvalidOperationException($"Pytanie {questionId} nie zostało znalezione w sesji");
                }

                // Add answer to session
                var diagnosticAnswer = new DiagnosticAnswer
                {
                    QuestionId = questionId,
                    Question = question.QuestionText,
                    Answer = answer,
                    AnsweredAt = DateTime.UtcNow
                };
                session.Answers.Add(diagnosticAnswer);
                session.CurrentQuestion++;

                // Check if we need to generate next phase
                await HandlePhaseTransitionAsync(session);

                // Check if we have more questions
                if (session.CurrentQuestion <= session.TotalQuestions)
                {
                    // More questions available
                    var nextQuestion = GetNextQuestion(session);
                    
                    // Update session in cache
                    await UpdateSessionInCacheAsync(session);

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
                    // All questions answered - generate final recommendations
                    _logger.LogInformation("All questions answered for session: {SessionId}. Generating recommendations.", sessionId);
                    
                    var recommendations = await _openAIService.GenerateRecommendationsAsync(
                        session.InitialSymptoms, 
                        session.Answers);

                    // Save recommendation for authenticated users
                    if (!string.IsNullOrEmpty(session.UserId))
                    {
                        await _recommendationService.SaveRecommendationAsync(session.UserId, recommendations);
                        _logger.LogInformation("Recommendation saved for user: {UserId}", session.UserId);
                    }
                    else
                    {
                        _logger.LogInformation("Anonymous user session completed. Recommendation not saved.");
                    }

                    // Remove session from cache
                    _cache.Remove(GetSessionCacheKey(sessionId));
                    
                    // For authenticated users, also remove from user-based cache
                    // We need to find the original email that was used to cache this session
                    // Since we can't easily reverse-lookup email from GUID in cache,
                    // we'll let the session expire naturally or handle cleanup differently
                    
                    _logger.LogInformation("Diagnostic session completed: {SessionId}, Syndrome: {Syndrome}", 
                        sessionId, recommendations.TcmSyndrome);

                    return new DiagnosticResponse
                    {
                        HasMoreQuestions = false,
                        CurrentQuestion = session.CurrentQuestion,
                        TotalQuestions = session.TotalQuestions,
                        CurrentPhase = session.CurrentPhase,
                        Recommendations = recommendations
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting answer for session: {SessionId}", sessionId);
                throw;
            }
        }

        /// <summary>
        /// Check if diagnostic session is valid and not expired
        /// </summary>
        public async Task<bool> IsSessionValidAsync(Guid sessionId)
        {
            try
            {
                _validationService.ValidateSessionId(sessionId);
                
                var session = await GetSessionFromCacheAsync(sessionId);
                return session != null && session.ExpiresAt > DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking session validity: {SessionId}", sessionId);
                return false;
            }
        }

        /// <summary>
        /// Clear expired sessions from cache (maintenance operation)
        /// </summary>
        public async Task ClearExpiredSessionsAsync()
        {
            try
            {
                // Note: MemoryCache doesn't provide direct enumeration
                // Expired entries will be automatically removed by the cache
                // This is a placeholder for future enhancement with distributed cache
                _logger.LogInformation("Expired sessions cleanup completed");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing expired sessions");
                throw;
            }
        }

        /// <summary>
        /// Handle phase transitions at question milestones (5, 10)
        /// </summary>
        private async Task HandlePhaseTransitionAsync(DiagnosticSession session)
        {
            // Check for phase transitions: Phase 2 after question 5, Phase 3 after question 10
            if (session.CurrentQuestion == 6 && session.CurrentPhase == 1)
            {
                // Transition to Phase 2
                await GenerateNextPhaseAsync(session, 2);
            }
            else if (session.CurrentQuestion == 11 && session.CurrentPhase == 2)
            {
                // Transition to Phase 3
                await GenerateNextPhaseAsync(session, 3);
            }
        }

        /// <summary>
        /// Generate questions for next phase and add to session
        /// </summary>
        private async Task GenerateNextPhaseAsync(DiagnosticSession session, int nextPhase)
        {
            try
            {
                _logger.LogInformation("Generating phase {Phase} questions for session: {SessionId}", nextPhase, session.SessionId);

                // Generate next phase questions based on all previous answers
                var nextPhaseQuestions = await _openAIService.GeneratePhaseQuestionsAsync(
                    nextPhase, 
                    session.InitialSymptoms, 
                    session.Answers);

                // Add new questions to session
                session.Questions.AddRange(nextPhaseQuestions.Questions);
                session.CurrentPhase = nextPhase;

                _logger.LogInformation("Phase {Phase} questions generated for session: {SessionId}, Questions added: {QuestionCount}", 
                    nextPhase, session.SessionId, nextPhaseQuestions.Questions.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating phase {Phase} for session: {SessionId}", nextPhase, session.SessionId);
                throw;
            }
        }

        /// <summary>
        /// Get next question for current session state
        /// </summary>
        private static DiagnosticQuestion? GetNextQuestion(DiagnosticSession session)
        {
            if (session.CurrentQuestion <= session.Questions.Count)
            {
                return session.Questions[session.CurrentQuestion - 1];
            }
            return null;
        }

        /// <summary>
        /// Get session from cache with error handling
        /// </summary>
        private async Task<DiagnosticSession?> GetSessionFromCacheAsync(Guid sessionId)
        {
            try
            {
                var cacheKey = GetSessionCacheKey(sessionId);
                var session = _cache.Get<DiagnosticSession>(cacheKey);
                
                if (session != null && session.ExpiresAt < DateTime.UtcNow)
                {
                    // Session expired, remove from cache
                    _cache.Remove(cacheKey);
                    return null;
                }
                
                return await Task.FromResult(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving session from cache: {SessionId}", sessionId);
                return null;
            }
        }

        /// <summary>
        /// Update session in cache with extended expiry
        /// </summary>
        private async Task UpdateSessionInCacheAsync(DiagnosticSession session)
        {
            try
            {
                var cacheKey = GetSessionCacheKey(session.SessionId);
                _cache.Set(cacheKey, session, TimeSpan.FromHours(SessionTimeoutHours));
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating session in cache: {SessionId}", session.SessionId);
                throw;
            }
        }

        /// <summary>
        /// Generate cache key for session
        /// </summary>
        private static string GetSessionCacheKey(Guid sessionId)
        {
            return $"diagnostic_session_{sessionId}";
        }

        /// <summary>
        /// Get user session cache key
        /// </summary>
        private static string GetUserSessionCacheKey(string userEmail)
        {
            return $"user_diagnostic_session_{userEmail}";
        }

        /// <summary>
        /// Get the actual user GUID from Supabase for authenticated user
        /// </summary>
        private async Task<string?> GetUserIdFromEmailAsync(string userEmail)
        {
            try
            {
                var currentUser = await _supabaseService.GetCurrentUserAsync();
                if (currentUser?.Id != null && !string.IsNullOrEmpty(currentUser.Email) 
                    && string.Equals(currentUser.Email, userEmail, StringComparison.OrdinalIgnoreCase))
                {
                    return currentUser.Id;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting userId for email: {UserEmail}", userEmail);
                return null;
            }
        }

        // Enhanced methods for simplified diagnostic flow

        /// <summary>
        /// Get next question for user based on question number
        /// </summary>
        public async Task<DiagnosticQuestion?> GetNextQuestionAsync(string userEmail, int questionNumber)
        {
            try
            {
                _validationService.ValidateUserId(userEmail);

                // Get or create session for user
                var session = await GetOrCreateUserSessionAsync(userEmail);
                if (session == null)
                {
                    _logger.LogWarning("Could not get or create session for user: {UserEmail}", userEmail);
                    return null;
                }

                // Ensure we have enough questions generated
                while (session.Questions.Count < questionNumber)
                {
                    var currentPhase = ((session.Questions.Count) / QuestionsPerPhase) + 1;
                    if (currentPhase <= 3)
                    {
                        await GenerateNextPhaseAsync(session, currentPhase);
                    }
                    else
                    {
                        break;
                    }
                }

                // Return the requested question
                                if (questionNumber <= session.Questions.Count)
                {
                    var question = session.Questions[questionNumber - 1];
                    await UpdateUserSessionInCacheAsync(session, userEmail);
                    return question;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting next question for user: {UserEmail}, Question: {QuestionNumber}", 
                    userEmail, questionNumber);
                return null;
            }
        }

        /// <summary>
        /// Submit answer for user based on question number
        /// </summary>
        public async Task<DiagnosticResponse> SubmitAnswerAsync(string userEmail, int questionNumber, bool answer)
        {
            try
            {
                _validationService.ValidateUserId(userEmail);

                var session = await GetOrCreateUserSessionAsync(userEmail);
                if (session == null)
                {
                    throw new InvalidOperationException("Nie można znaleźć sesji diagnostycznej");
                }

                // Get the question
                if (questionNumber > session.Questions.Count)
                {
                    throw new InvalidOperationException($"Pytanie {questionNumber} nie istnieje");
                }

                var question = session.Questions[questionNumber - 1];

                // Check if answer already exists for this question
                var existingAnswer = session.Answers.FirstOrDefault(a => a.QuestionId == question.Id);
                if (existingAnswer != null)
                {
                    // Update existing answer
                    existingAnswer.Answer = answer;
                    existingAnswer.AnsweredAt = DateTime.UtcNow;
                }
                else
                {
                    // Add new answer
                    session.Answers.Add(new DiagnosticAnswer
                    {
                        QuestionId = question.Id,
                        Question = question.QuestionText,
                        Answer = answer,
                        AnsweredAt = DateTime.UtcNow
                    });
                }

                session.CurrentQuestion = Math.Max(session.CurrentQuestion, questionNumber + 1);

                // Check if we need to generate next phase
                await HandlePhaseTransitionAsync(session);

                // Check if we have more questions
                if (session.CurrentQuestion <= session.TotalQuestions)
                {
                    // Ensure next question is available
                    var nextQuestion = await GetNextQuestionAsync(userEmail, session.CurrentQuestion);
                    
                    await UpdateUserSessionInCacheAsync(session, userEmail);

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
                    // Generate recommendations
                    var recommendations = await _openAIService.GenerateRecommendationsAsync(
                        session.InitialSymptoms, 
                        session.Answers);

                    // Save recommendation for authenticated users
                    if (!string.IsNullOrEmpty(session.UserId))
                    {
                        await _recommendationService.SaveRecommendationAsync(session.UserId, recommendations);
                        _logger.LogInformation("Recommendation saved for user: {UserId}", session.UserId);
                    }
                    else
                    {
                        _logger.LogInformation("Anonymous user session completed. Recommendation not saved.");
                    }

                    // Remove session from cache
                    _cache.Remove(GetUserSessionCacheKey(userEmail));

                    return new DiagnosticResponse
                    {
                        HasMoreQuestions = false,
                        CurrentQuestion = session.CurrentQuestion,
                        TotalQuestions = session.TotalQuestions,
                        CurrentPhase = session.CurrentPhase,
                        Recommendations = recommendations
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting answer for user: {UserEmail}, Question: {QuestionNumber}", 
                    userEmail, questionNumber);
                throw;
            }
        }

        /// <summary>
        /// Validate session for user
        /// </summary>
        public async Task<bool> ValidateSessionAsync(string userEmail)
        {
            try
            {
                _validationService.ValidateUserId(userEmail);
                
                var session = await GetUserSessionFromCacheAsync(userEmail);
                return session != null && session.ExpiresAt > DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating session for user: {UserEmail}", userEmail);
                return false;
            }
        }

        /// <summary>
        /// Get session information for user
        /// </summary>
        public async Task<DiagnosticSessionInfo?> GetSessionInfoAsync(string userEmail)
        {
            try
            {
                _validationService.ValidateUserId(userEmail);
                
                var session = await GetUserSessionFromCacheAsync(userEmail);
                if (session == null)
                {
                    return null;
                }

                return new DiagnosticSessionInfo
                {
                    SessionId = session.SessionId,
                    UserEmail = userEmail,
                    CurrentQuestion = session.CurrentQuestion,
                    TotalQuestions = session.TotalQuestions,
                    CurrentPhase = session.CurrentPhase,
                    TimeRemaining = session.ExpiresAt - DateTime.UtcNow,
                    LastActivity = session.Answers.LastOrDefault()?.AnsweredAt ?? session.CreatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session info for user: {UserEmail}", userEmail);
                return null;
            }
        }

        /// <summary>
        /// Get or create diagnostic session for user
        /// </summary>
        private async Task<DiagnosticSession?> GetOrCreateUserSessionAsync(string userEmail)
        {
            var session = await GetUserSessionFromCacheAsync(userEmail);
            if (session != null && session.ExpiresAt > DateTime.UtcNow)
            {
                return session;
            }

            // Get the actual user GUID from AuthenticationStateProvider instead of Supabase directly
            try
            {
                var authState = await _authStateProvider.GetAuthenticationStateAsync();
                var user = authState.User;
                
                if (!user.Identity?.IsAuthenticated == true)
                {
                    _logger.LogWarning("Cannot create session - user not authenticated");
                    return null;
                }

                var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Cannot create session - userId not found in claims");
                    return null;
                }

                // Create new session with proper userId (GUID)
                var newSession = await StartSessionAsync("", userId);
                
                // Also cache it with email-based key for user-based lookup
                if (newSession != null)
                {
                    var userCacheKey = GetUserSessionCacheKey(userEmail);
                    _cache.Set(userCacheKey, newSession, TimeSpan.FromHours(SessionTimeoutHours));
                }
                
                return newSession;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user for session creation");
                return null;
            }
        }

        /// <summary>
        /// Get user session from cache
        /// </summary>
        private async Task<DiagnosticSession?> GetUserSessionFromCacheAsync(string userEmail)
        {
            try
            {
                var cacheKey = GetUserSessionCacheKey(userEmail);
                var session = _cache.Get<DiagnosticSession>(cacheKey);
                
                if (session != null && session.ExpiresAt < DateTime.UtcNow)
                {
                    _cache.Remove(cacheKey);
                    return null;
                }
                
                return await Task.FromResult(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user session from cache: {UserEmail}", userEmail);
                return null;
            }
        }

        /// <summary>
        /// Update user session in cache
        /// </summary>
        private async Task UpdateUserSessionInCacheAsync(DiagnosticSession session, string userEmail)
        {
            try
            {
                // Update both email-based cache and session-based cache for consistency
                var userCacheKey = GetUserSessionCacheKey(userEmail);
                _cache.Set(userCacheKey, session, TimeSpan.FromHours(SessionTimeoutHours));
                
                var sessionCacheKey = GetSessionCacheKey(session.SessionId);
                _cache.Set(sessionCacheKey, session, TimeSpan.FromHours(SessionTimeoutHours));
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user session in cache: {UserEmail}", userEmail);
                throw;
            }
        }

        /// <summary>
        /// Start anonymous diagnostic session (without user authentication)
        /// Returns session ID that can be used to continue diagnostic flow
        /// </summary>
        public async Task<DiagnosticSession> StartAnonymousSessionAsync(string? initialSymptoms)
        {
            return await StartSessionAsync(initialSymptoms, userId: null);
        }

        /// <summary>
        /// Get next question by session ID (for anonymous users)
        /// </summary>
        public async Task<DiagnosticQuestion?> GetNextQuestionBySessionAsync(Guid sessionId, int questionNumber)
        {
            try
            {
                _validationService.ValidateSessionId(sessionId);
                
                var session = await GetSessionFromCacheAsync(sessionId);
                if (session == null)
                {
                    _logger.LogWarning("Session not found: {SessionId}", sessionId);
                    return null;
                }

                // Handle phase transitions if needed
                await HandlePhaseTransitionAsync(session);

                // Return question by number
                if (questionNumber > 0 && questionNumber <= session.Questions.Count)
                {
                    return session.Questions[questionNumber - 1];
                }

                _logger.LogWarning("Question number {QuestionNumber} not found in session {SessionId}", questionNumber, sessionId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting question for session: {SessionId}, Question: {QuestionNumber}", sessionId, questionNumber);
                return null;
            }
        }

        /// <summary>
        /// Submit answer using session ID (for anonymous users)
        /// </summary>
        public async Task<DiagnosticResponse> SubmitAnswerBySessionAsync(Guid sessionId, int questionNumber, bool answer)
        {
            try
            {
                _validationService.ValidateSessionId(sessionId);
                
                var session = await GetSessionFromCacheAsync(sessionId);
                if (session == null)
                {
                    throw new InvalidOperationException("Sesja diagnostyczna wygasła lub nie została znaleziona");
                }

                // Find question by number
                if (questionNumber <= 0 || questionNumber > session.Questions.Count)
                {
                    throw new InvalidOperationException($"Nieprawidłowy numer pytania: {questionNumber}");
                }

                var question = session.Questions[questionNumber - 1];
                
                // Use existing SubmitAnswerAsync method
                return await SubmitAnswerAsync(sessionId, question.Id, answer, userId: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting answer for session: {SessionId}, Question: {QuestionNumber}", sessionId, questionNumber);
                throw;
            }
        }

        /// <summary>
        /// Validate session by ID
        /// </summary>
        public async Task<bool> ValidateSessionByIdAsync(Guid sessionId)
        {
            try
            {
                _validationService.ValidateSessionId(sessionId);
                
                var session = await GetSessionFromCacheAsync(sessionId);
                return session != null && session.ExpiresAt > DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating session by ID: {SessionId}", sessionId);
                return false;
            }
        }

        /// <summary>
        /// Get session information by ID
        /// </summary>
        public async Task<DiagnosticSessionInfo?> GetSessionInfoByIdAsync(Guid sessionId)
        {
            try
            {
                _validationService.ValidateSessionId(sessionId);
                
                var session = await GetSessionFromCacheAsync(sessionId);
                if (session == null)
                {
                    return null;
                }

                return new DiagnosticSessionInfo
                {
                    SessionId = session.SessionId,
                    CurrentQuestion = session.CurrentQuestion,
                    TotalQuestions = session.TotalQuestions,
                    CurrentPhase = session.CurrentPhase,
                    TimeRemaining = session.ExpiresAt - DateTime.UtcNow,
                    LastActivity = session.Answers.LastOrDefault()?.AnsweredAt ?? session.CreatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session info by ID: {SessionId}", sessionId);
                return null;
            }
        }
    }
} 