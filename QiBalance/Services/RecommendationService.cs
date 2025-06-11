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

        public RecommendationService(
            IDatabaseContext databaseContext,
            IValidationService validationService,
            ILogger<RecommendationService> logger,
            IAuthService authService)
        {
            _databaseContext = databaseContext;
            _validationService = validationService;
            _logger = logger;
            _authService = authService;
        }

        /// <summary>
        /// Save AI-generated recommendation for authenticated user
        /// Creates or updates recommendation entity with user authorization
        /// </summary>
        public async Task<RecommendationEntity> SaveRecommendationAsync(string userId, RecommendationResult recommendationResult)
        {
            try
            {
                var currentUserId = await _authService.GetCurrentUserIdAsync();
                if (currentUserId == null || !currentUserId.Equals(userId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException("User is not authorized to save this recommendation.");
                }
                
                _validationService.ValidateUserId(userId);
                _validationService.ValidateRecommendationText(recommendationResult.RecommendationText);

                _logger.LogInformation("Saving recommendation for user: {UserId}, Syndrome: {TcmSyndrome}", 
                    userId, recommendationResult.TcmSyndrome);

                var recommendationInsert = new RecommendationInsert
                {
                    UserId = Guid.Parse(userId),
                    RecommendationText = recommendationResult.RecommendationText,
                    DateGenerated = DateTime.UtcNow
                };

                var savedRecommendation = await _databaseContext.CreateRecommendationAsync(recommendationInsert);

                var recommendationEntity = MapToRecommendationEntity(savedRecommendation, userId);

                _logger.LogInformation("Recommendation saved successfully: {RecommendationId} for user: {UserId}", 
                    recommendationEntity.RecommendationId, userId);

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
                var currentUserId = await _authService.GetCurrentUserIdAsync();
                if (currentUserId == null || !currentUserId.Equals(userId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException("User is not authorized to get recommendations.");
                }

                _validationService.ValidateUserId(userId);
                _validationService.ValidatePagination(page, limit);

                _logger.LogInformation("Getting recommendations for user: {UserId}, Page: {Page}, Limit: {Limit}, Sort: {Sort}", 
                    userId, page, limit, sort);

                var recommendations = await _databaseContext.GetRecommendationsAsync(userId);
                
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
                var currentUserId = await _authService.GetCurrentUserIdAsync();
                if (currentUserId == null || !currentUserId.Equals(userId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException("User is not authorized to get this recommendation.");
                }

                _validationService.ValidateUserId(userId);
                
                if (recommendationId == Guid.Empty)
                {
                    throw new ValidationException("Identyfikator rekomendacji jest nieprawidłowy");
                }

                _logger.LogInformation("Getting recommendation: {RecommendationId} for user: {UserId}", 
                    recommendationId, userId);

                var recommendation = await _databaseContext.GetRecommendationAsync(recommendationId, userId);

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
                var currentUserId = await _authService.GetCurrentUserIdAsync();
                if (currentUserId == null || !currentUserId.Equals(userId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException("User is not authorized to delete this recommendation.");
                }
                
                _validationService.ValidateUserId(userId);
                
                if (recommendationId == Guid.Empty)
                {
                    throw new ValidationException("Identyfikator rekomendacji jest nieprawidłowy");
                }

                _logger.LogInformation("Deleting recommendation: {RecommendationId} for user: {UserId}", 
                    recommendationId, userId);
                
                return await _databaseContext.DeleteRecommendationAsync(recommendationId, userId);
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
                UserId = originalUserId, // Keep original user identifier (email or guid string)
                DateGenerated = recommendation.DateGenerated,
                RecommendationText = recommendation.RecommendationText
            };
        }
    }
} 