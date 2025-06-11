using QiBalance.Models;
using QiBalance.Models.DTOs;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace QiBalance.Services
{
    /// <summary>
    /// Interface for recommendation service with user authorization and CRUD operations
    /// Manages AI-generated recommendations with pagination and security
    /// </summary>
    public interface IRecommendationService
    {
        Task<RecommendationEntity> SaveRecommendationAsync(string userId, RecommendationResult recommendationResult);
        Task<PagedResult<RecommendationEntity>> GetUserRecommendationsAsync(
            string userId, 
            int page = 1, 
            int limit = 10, 
            SortOrder sort = SortOrder.DateDesc);
        Task<RecommendationEntity?> GetRecommendationByIdAsync(Guid recommendationId, string userId);
        Task<bool> DeleteRecommendationAsync(Guid recommendationId, string userId);
    }

    /// <summary>
    /// Service providing CRUD operations for user recommendations with RLS security
    /// Handles recommendation persistence, retrieval with pagination, and user authorization
    /// </summary>
    public class RecommendationService : IRecommendationService
    {
        private readonly IDatabaseContext _databaseContext;
        private readonly IValidationService _validationService;
        private readonly ILogger<RecommendationService> _logger;
        private readonly IAuthService _authService;
        private readonly AuthenticationStateProvider _authStateProvider;

        public RecommendationService(
            IDatabaseContext databaseContext,
            IValidationService validationService,
            ILogger<RecommendationService> logger,
            IAuthService authService,
            AuthenticationStateProvider authStateProvider)
        {
            _databaseContext = databaseContext;
            _validationService = validationService;
            _logger = logger;
            _authService = authService;
            _authStateProvider = authStateProvider;
        }

        /// <summary>
        /// Save AI-generated recommendation for authenticated user
        /// Creates or updates recommendation entity with user authorization
        /// </summary>
        public async Task<RecommendationEntity> SaveRecommendationAsync(string userId, RecommendationResult recommendationResult)
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                
                // If no current user but userId is a valid GUID or email, this might be a session timeout during diagnostic
                if (currentUser == null && (Guid.TryParse(userId, out var userGuid) || userId.Contains("@")))
                {
                    _logger.LogWarning("No current user session found, but userId is valid. Proceeding with fallback save for: {UserId}", userId);
                    
                    _validationService.ValidateRecommendationText(recommendationResult.RecommendationText);
                    _logger.LogInformation("Saving recommendation (fallback) for user: {UserId}, Syndrome: {TcmSyndrome}", userId, recommendationResult.TcmSyndrome);

                    // For email, we need to find the user's UUID from the database or use a placeholder
                    Guid fallbackUserId;
                    if (Guid.TryParse(userId, out userGuid))
                    {
                        fallbackUserId = userGuid;
                    }
                    else
                    {
                        // For email-based fallback, get the user's UUID from AuthenticationStateProvider
                        var fallbackUserIdString = await GetUserIdFromAuthStateAsync(userId);
                        if (!string.IsNullOrEmpty(fallbackUserIdString) && Guid.TryParse(fallbackUserIdString, out var fallbackUserGuidFromAuth))
                        {
                            fallbackUserId = fallbackUserGuidFromAuth;
                            _logger.LogInformation("Using UUID {UserId} from auth state for email {Email}", fallbackUserId, userId);
                        }
                        else
                        {
                            _logger.LogWarning("Cannot save recommendation for email {Email} - user UUID not found and session expired", userId);
                            // Return a dummy recommendation entity since we can't save to database
                            return new RecommendationEntity
                            {
                                RecommendationId = Guid.NewGuid(),
                                UserId = userId,
                                DateGenerated = DateTime.UtcNow,
                                RecommendationText = recommendationResult.RecommendationText
                            };
                        }
                    }

                    var fallbackRecommendationInsert = new RecommendationInsert
                    {
                        UserId = fallbackUserId,
                        RecommendationText = recommendationResult.RecommendationText,
                        DateGenerated = DateTime.UtcNow
                    };

                    var fallbackSavedRecommendation = await _databaseContext.CreateRecommendationAsync(fallbackRecommendationInsert);
                    if (fallbackSavedRecommendation == null)
                        throw new InvalidOperationException("Failed to save recommendation - no data returned from database");
                        
                    var fallbackRecommendationEntity = MapToRecommendationEntity(fallbackSavedRecommendation, userId);

                    _logger.LogInformation("Recommendation saved successfully (fallback): {RecommendationId} for user: {UserId}", fallbackRecommendationEntity.RecommendationId, userId);
                    return fallbackRecommendationEntity;
                }
                
                if (currentUser == null)
                    throw new UnauthorizedAccessException("User is not authenticated.");

                bool isAuthorized = currentUser.Id.Equals(userId, StringComparison.OrdinalIgnoreCase) ||
                                      (currentUser.Email != null && currentUser.Email.Equals(userId, StringComparison.OrdinalIgnoreCase));

                if (!isAuthorized)
                    throw new UnauthorizedAccessException("User is not authorized to save this recommendation.");

                _validationService.ValidateRecommendationText(recommendationResult.RecommendationText);
                _logger.LogInformation("Saving recommendation for user: {UserId}, Syndrome: {TcmSyndrome}", userId, recommendationResult.TcmSyndrome);

                var recommendationInsert = new RecommendationInsert
                {
                    UserId = Guid.Parse(currentUser.Id),
                    RecommendationText = recommendationResult.RecommendationText,
                    DateGenerated = DateTime.UtcNow
                };

                var savedRecommendation = await _databaseContext.CreateRecommendationAsync(recommendationInsert);
                if (savedRecommendation == null)
                    throw new InvalidOperationException("Failed to save recommendation - no data returned from database");
                    
                var recommendationEntity = MapToRecommendationEntity(savedRecommendation, userId);

                _logger.LogInformation("Recommendation saved successfully: {RecommendationId} for user: {UserId}", recommendationEntity.RecommendationId, userId);
                return recommendationEntity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving recommendation for user: {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Get paginated recommendations for user with sorting options
        /// Supports date-based sorting and pagination for performance
        /// </summary>
        public async Task<PagedResult<RecommendationEntity>> GetUserRecommendationsAsync(
            string userId, 
            int page = 1, 
            int limit = 10, 
            SortOrder sort = SortOrder.DateDesc)
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                
                // If no current user but userId is a valid GUID or email, this might be a session timeout
                if (currentUser == null && (Guid.TryParse(userId, out var userGuid) || userId.Contains("@")))
                {
                    _logger.LogWarning("No current user session found, but userId is valid. Proceeding with fallback read for: {UserId}", userId);
                    
                    _validationService.ValidatePagination(page, limit);
                    _logger.LogInformation("Getting recommendations (fallback) for user: {UserId}, Page: {Page}, Limit: {Limit}, Sort: {Sort}", userId, page, limit, sort);

                    // For UUID, we can query directly
                    if (Guid.TryParse(userId, out userGuid))
                    {
                        var fallbackRecommendations = await _databaseContext.GetRecommendationsAsync(userGuid.ToString());
                        
                        var fallbackTotalCount = fallbackRecommendations.Count();
                        var fallbackPagedItems = fallbackRecommendations
                            .Skip((page - 1) * limit)
                            .Take(limit)
                            .Select(r => MapToRecommendationEntity(r, userId))
                            .ToList();

                        var fallbackResult = new PagedResult<RecommendationEntity>
                        {
                            Items = fallbackPagedItems,
                            TotalCount = fallbackTotalCount,
                            Page = page,
                            Limit = limit
                        };

                        _logger.LogInformation("Retrieved {Count} recommendations (fallback) for user: {UserId}", fallbackResult.Items.Count, userId);
                        return fallbackResult;
                    }
                    else
                    {
                        // For email-based fallback, get the user's UUID from AuthenticationStateProvider
                        var fallbackUserIdString = await GetUserIdFromAuthStateAsync(userId);
                        if (!string.IsNullOrEmpty(fallbackUserIdString) && Guid.TryParse(fallbackUserIdString, out var fallbackUserGuidFromAuth))
                        {
                            var fallbackRecommendations = await _databaseContext.GetRecommendationsAsync(fallbackUserIdString);
                            
                            var fallbackTotalCount = fallbackRecommendations.Count();
                            var fallbackPagedItems = fallbackRecommendations
                                .Skip((page - 1) * limit)
                                .Take(limit)
                                .Select(r => MapToRecommendationEntity(r, userId))
                                .ToList();

                            var fallbackResult = new PagedResult<RecommendationEntity>
                            {
                                Items = fallbackPagedItems,
                                TotalCount = fallbackTotalCount,
                                Page = page,
                                Limit = limit
                            };

                            _logger.LogInformation("Retrieved {Count} recommendations (fallback) from auth state for email: {Email}", fallbackResult.Items.Count, userId);
                            return fallbackResult;
                        }
                        else
                        {
                            // For email-based fallback, return empty result since we can't get UUID
                            _logger.LogWarning("Cannot retrieve recommendations for email {Email} - user UUID not found and session expired", userId);
                            return new PagedResult<RecommendationEntity>
                            {
                                Items = new List<RecommendationEntity>(),
                                TotalCount = 0,
                                Page = page,
                                Limit = limit
                            };
                        }
                    }
                }
                
                if (currentUser == null)
                    throw new UnauthorizedAccessException("User is not authenticated.");

                bool isAuthorized = currentUser.Id.Equals(userId, StringComparison.OrdinalIgnoreCase) ||
                                      (currentUser.Email != null && currentUser.Email.Equals(userId, StringComparison.OrdinalIgnoreCase));

                if (!isAuthorized)
                    throw new UnauthorizedAccessException("User is not authorized to get recommendations.");

                _validationService.ValidatePagination(page, limit);
                _logger.LogInformation("Getting recommendations for user: {UserId}, Page: {Page}, Limit: {Limit}, Sort: {Sort}", userId, page, limit, sort);

                // Always use the authenticated user's GUID for database queries
                var recommendations = await _databaseContext.GetRecommendationsAsync(currentUser.Id);
                
                var totalCount = recommendations.Count();
                var pagedItems = recommendations
                    .Skip((page - 1) * limit)
                    .Take(limit)
                    .Select(r => MapToRecommendationEntity(r, userId))
                    .ToList();

                var result = new PagedResult<RecommendationEntity>
                {
                    Items = pagedItems,
                    TotalCount = totalCount,
                    Page = page,
                    Limit = limit
                };

                _logger.LogInformation("Retrieved {Count} recommendations for user: {UserId}", result.Items.Count, userId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recommendations for user: {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Get specific recommendation by ID with user authorization check
        /// Ensures user can only access their own recommendations
        /// </summary>
        public async Task<RecommendationEntity?> GetRecommendationByIdAsync(Guid recommendationId, string userId)
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                
                // If no current user but userId is a valid GUID or email, this might be a session timeout
                if (currentUser == null && (Guid.TryParse(userId, out var userGuid) || userId.Contains("@")))
                {
                    _logger.LogWarning("No current user session found, but userId is valid. Proceeding with fallback read for: {UserId}", userId);
                    
                    if (recommendationId == Guid.Empty)
                        throw new ValidationException("Identyfikator rekomendacji jest nieprawidłowy");

                    _logger.LogInformation("Getting recommendation (fallback): {RecommendationId} for user: {UserId}", recommendationId, userId);

                    // For UUID, we can query directly
                    if (Guid.TryParse(userId, out userGuid))
                    {
                        var fallbackRecommendation = await _databaseContext.GetRecommendationAsync(recommendationId, userGuid.ToString());
                        if (fallbackRecommendation == null) return null;
                        return MapToRecommendationEntity(fallbackRecommendation, userId);
                    }
                    else
                    {
                        // For email-based fallback, get the user's UUID from AuthenticationStateProvider
                        var fallbackUserIdString = await GetUserIdFromAuthStateAsync(userId);
                        if (!string.IsNullOrEmpty(fallbackUserIdString))
                        {
                            var fallbackRecommendation = await _databaseContext.GetRecommendationAsync(recommendationId, fallbackUserIdString);
                            if (fallbackRecommendation == null) return null;
                            return MapToRecommendationEntity(fallbackRecommendation, userId);
                        }
                        else
                        {
                            // For email-based fallback, return null since we can't get UUID
                            _logger.LogWarning("Cannot retrieve recommendation for email {Email} - user UUID not found and session expired", userId);
                            return null;
                        }
                    }
                }
                
                if (currentUser == null)
                    throw new UnauthorizedAccessException("User is not authenticated.");

                bool isAuthorized = currentUser.Id.Equals(userId, StringComparison.OrdinalIgnoreCase) ||
                                      (currentUser.Email != null && currentUser.Email.Equals(userId, StringComparison.OrdinalIgnoreCase));

                if (!isAuthorized)
                    throw new UnauthorizedAccessException("User is not authorized to get this recommendation.");

                if (recommendationId == Guid.Empty)
                    throw new ValidationException("Identyfikator rekomendacji jest nieprawidłowy");

                _logger.LogInformation("Getting recommendation: {RecommendationId} for user: {UserId}", recommendationId, userId);

                // Always use the authenticated user's GUID for database queries
                var recommendation = await _databaseContext.GetRecommendationAsync(recommendationId, currentUser.Id);

                if (recommendation == null) return null;

                return MapToRecommendationEntity(recommendation, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recommendation by ID for user: {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Delete recommendation with user authorization
        /// Soft delete approach maintains audit trail
        /// </summary>
        public async Task<bool> DeleteRecommendationAsync(Guid recommendationId, string userId)
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                
                // If no current user but userId is a valid GUID or email, this might be a session timeout
                if (currentUser == null && (Guid.TryParse(userId, out var userGuid) || userId.Contains("@")))
                {
                    _logger.LogWarning("No current user session found, but userId is valid. Proceeding with fallback delete for: {UserId}", userId);
                    
                    if (recommendationId == Guid.Empty)
                        throw new ValidationException("Identyfikator rekomendacji jest nieprawidłowy");

                    _logger.LogInformation("Deleting recommendation (fallback): {RecommendationId} for user: {UserId}", recommendationId, userId);
                    
                    // For UUID, we can delete directly
                    if (Guid.TryParse(userId, out userGuid))
                    {
                        return await _databaseContext.DeleteRecommendationAsync(recommendationId, userGuid.ToString());
                    }
                    else
                    {
                        // For email-based fallback, get the user's UUID from AuthenticationStateProvider
                        var fallbackUserIdString = await GetUserIdFromAuthStateAsync(userId);
                        if (!string.IsNullOrEmpty(fallbackUserIdString))
                        {
                            return await _databaseContext.DeleteRecommendationAsync(recommendationId, fallbackUserIdString);
                        }
                        else
                        {
                            // For email-based fallback, return false since we can't get UUID
                            _logger.LogWarning("Cannot delete recommendation for email {Email} - user UUID not found and session expired", userId);
                            return false;
                        }
                    }
                }
                
                if (currentUser == null)
                    throw new UnauthorizedAccessException("User is not authenticated.");
                
                bool isAuthorized = currentUser.Id.Equals(userId, StringComparison.OrdinalIgnoreCase) ||
                                      (currentUser.Email != null && currentUser.Email.Equals(userId, StringComparison.OrdinalIgnoreCase));
                
                if (!isAuthorized)
                    throw new UnauthorizedAccessException("User is not authorized to delete this recommendation.");
                
                if (recommendationId == Guid.Empty)
                    throw new ValidationException("Identyfikator rekomendacji jest nieprawidłowy");

                _logger.LogInformation("Deleting recommendation: {RecommendationId} for user: {UserId}", recommendationId, userId);
                
                // Always use the authenticated user's GUID for database queries
                return await _databaseContext.DeleteRecommendationAsync(recommendationId, currentUser.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting recommendation for user: {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Maps database model to entity model for UI
        /// </summary>
        private static RecommendationEntity MapToRecommendationEntity(Recommendation recommendation, string originalUserId)
        {
            return new RecommendationEntity
            {
                RecommendationId = recommendation.RecommendationId,
                UserId = originalUserId,
                DateGenerated = recommendation.DateGenerated,
                RecommendationText = recommendation.RecommendationText
            };
        }

        /// <summary>
        /// Get user ID from AuthenticationStateProvider when Supabase session is expired
        /// </summary>
        private async Task<string?> GetUserIdFromAuthStateAsync(string userEmail)
        {
            try
            {
                var authState = await _authStateProvider.GetAuthenticationStateAsync();
                var user = authState.User;
                
                if (user.Identity?.IsAuthenticated == true)
                {
                    var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    var emailClaim = user.FindFirst(ClaimTypes.Email)?.Value;
                    
                    // Verify that the email matches
                    if (!string.IsNullOrEmpty(userIdClaim) && 
                        !string.IsNullOrEmpty(emailClaim) && 
                        string.Equals(emailClaim, userEmail, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Retrieved user ID {UserId} from auth state for email {Email}", userIdClaim, userEmail);
                        return userIdClaim;
                    }
                }
                
                _logger.LogWarning("Could not retrieve user ID from auth state for email {Email}", userEmail);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user ID from auth state for email {Email}", userEmail);
                return null;
            }
        }
    }
} 