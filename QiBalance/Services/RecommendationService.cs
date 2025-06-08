using QiBalance.Models;
using QiBalance.Models.DTOs;
using System.ComponentModel.DataAnnotations;

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
        private readonly ISupabaseService _supabaseService;
        private readonly IValidationService _validationService;
        private readonly ILogger<RecommendationService> _logger;

        public RecommendationService(
            ISupabaseService supabaseService,
            IValidationService validationService,
            ILogger<RecommendationService> logger)
        {
            _supabaseService = supabaseService;
            _validationService = validationService;
            _logger = logger;
        }

        /// <summary>
        /// Save AI-generated recommendation for authenticated user
        /// Creates new recommendation entity with user authorization
        /// </summary>
        public async Task<RecommendationEntity> SaveRecommendationAsync(string userId, RecommendationResult recommendationResult)
        {
            try
            {
                // Validate input parameters
                _validationService.ValidateUserId(userId);
                _validationService.ValidateRecommendationText(recommendationResult.RecommendationText);

                if (string.IsNullOrWhiteSpace(recommendationResult.TcmSyndrome))
                {
                    throw new ValidationException("Syndrom TCM jest wymagany");
                }

                _logger.LogInformation("Saving recommendation for user: {UserId}, Syndrome: {TcmSyndrome}", 
                    userId, recommendationResult.TcmSyndrome);

                // Convert userId string to Guid (assuming Supabase uses Guid for user IDs)
                if (!Guid.TryParse(userId, out var userGuid))
                {
                    throw new ValidationException("Nieprawidłowy format identyfikatora użytkownika");
                }

                // Create recommendation model for database
                var recommendation = new Recommendation
                {
                    RecommendationId = Guid.NewGuid(),
                    UserId = userGuid,
                    DateGenerated = DateTime.UtcNow,
                    RecommendationText = recommendationResult.RecommendationText
                };

                // Save to database via Supabase (RLS automatically enforced)
                var savedRecommendation = await _supabaseService.InsertAsync(recommendation);

                // Convert to RecommendationEntity for UI
                var recommendationEntity = MapToRecommendationEntity(savedRecommendation, userId);

                _logger.LogInformation("Recommendation saved successfully: {RecommendationId}", recommendationEntity.RecommendationId);

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
                // Validate input parameters
                _validationService.ValidateUserId(userId);
                _validationService.ValidatePagination(page, limit);

                _logger.LogInformation("Getting recommendations for user: {UserId}, Page: {Page}, Limit: {Limit}, Sort: {Sort}", 
                    userId, page, limit, sort);

                // Convert userId to Guid
                if (!Guid.TryParse(userId, out var userGuid))
                {
                    throw new ValidationException("Nieprawidłowy format identyfikatora użytkownika");
                }

                // Build sort string for Supabase
                var sortBy = sort == SortOrder.DateDesc ? "date_generated desc" : "date_generated asc";

                // Get paginated results from Supabase
                var pagedResult = await _supabaseService.GetPagedAsync<Recommendation>(userGuid, page, limit, sortBy);

                // Convert to RecommendationEntity list
                var recommendationEntities = pagedResult.Items
                    .Select(r => MapToRecommendationEntity(r, userId))
                    .ToList();

                var result = new PagedResult<RecommendationEntity>
                {
                    Items = recommendationEntities,
                    TotalCount = pagedResult.TotalCount,
                    Page = pagedResult.Page,
                    Limit = pagedResult.Limit
                };

                _logger.LogInformation("Retrieved {Count} recommendations for user: {UserId}", 
                    result.Items.Count, userId);

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
                // Validate input parameters
                _validationService.ValidateUserId(userId);
                
                if (recommendationId == Guid.Empty)
                {
                    throw new ValidationException("Identyfikator rekomendacji jest nieprawidłowy");
                }

                _logger.LogInformation("Getting recommendation: {RecommendationId} for user: {UserId}", 
                    recommendationId, userId);

                // Get recommendation from database (RLS will enforce user access)
                var recommendation = await _supabaseService.GetByIdAsync<Recommendation>(recommendationId);

                if (recommendation == null)
                {
                    _logger.LogWarning("Recommendation not found: {RecommendationId}", recommendationId);
                    return null;
                }

                // Additional authorization check (belt and suspenders approach)
                if (recommendation.UserId.ToString() != userId)
                {
                    _logger.LogWarning("Unauthorized access attempt to recommendation: {RecommendationId} by user: {UserId}", 
                        recommendationId, userId);
                    throw new UnauthorizedAccessException("Brak dostępu do tej rekomendacji");
                }

                var recommendationEntity = MapToRecommendationEntity(recommendation, userId);

                _logger.LogInformation("Recommendation retrieved successfully: {RecommendationId}", recommendationId);

                return recommendationEntity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recommendation: {RecommendationId} for user: {UserId}", 
                    recommendationId, userId);
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
                // Validate input parameters
                _validationService.ValidateUserId(userId);
                
                if (recommendationId == Guid.Empty)
                {
                    throw new ValidationException("Identyfikator rekomendacji jest nieprawidłowy");
                }

                _logger.LogInformation("Deleting recommendation: {RecommendationId} for user: {UserId}", 
                    recommendationId, userId);

                // Convert userId to Guid for authorization
                if (!Guid.TryParse(userId, out var userGuid))
                {
                    throw new ValidationException("Nieprawidłowy format identyfikatora użytkownika");
                }

                // First verify the recommendation exists and belongs to user
                var recommendation = await _supabaseService.GetByIdAsync<Recommendation>(recommendationId);
                if (recommendation == null)
                {
                    _logger.LogWarning("Recommendation not found for deletion: {RecommendationId}", recommendationId);
                    return false;
                }

                if (recommendation.UserId != userGuid)
                {
                    _logger.LogWarning("Unauthorized deletion attempt for recommendation: {RecommendationId} by user: {UserId}", 
                        recommendationId, userId);
                    throw new UnauthorizedAccessException("Brak dostępu do tej rekomendacji");
                }

                // Delete from database (RLS enforced)
                var success = await _supabaseService.DeleteAsync<Recommendation>(recommendationId, userGuid);

                if (success)
                {
                    _logger.LogInformation("Recommendation deleted successfully: {RecommendationId}", recommendationId);
                }
                else
                {
                    _logger.LogWarning("Failed to delete recommendation: {RecommendationId}", recommendationId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting recommendation: {RecommendationId} for user: {UserId}", 
                    recommendationId, userId);
                throw;
            }
        }

        /// <summary>
        /// Map database Recommendation model to UI RecommendationEntity
        /// Provides clean separation between database and UI concerns
        /// </summary>
        private static RecommendationEntity MapToRecommendationEntity(Recommendation recommendation, string userId)
        {
            return new RecommendationEntity
            {
                RecommendationId = recommendation.RecommendationId,
                UserId = userId,
                DateGenerated = recommendation.DateGenerated,
                RecommendationText = recommendation.RecommendationText
            };
        }
    }
} 