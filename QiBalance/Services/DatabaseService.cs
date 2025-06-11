using QiBalance.Models;
using QiBalance.Services;

namespace QiBalance.Services
{
    public class DatabaseService : IDatabaseContext
    {
        private readonly ISupabaseService _supabaseService;
        private readonly ILogger<DatabaseService> _logger;
        
        public DatabaseService(ISupabaseService supabaseService, ILogger<DatabaseService> logger)
        {
            _supabaseService = supabaseService;
            _logger = logger;
        }

        public async Task<IEnumerable<Recommendation>> GetRecommendationsAsync(string userId)
        {
            try
            {
                var userGuid = Guid.Parse(userId);
                var response = await _supabaseService.Client
                    .From<Recommendation>()
                    .Where(r => r.UserId == userGuid)
                    .Order(r => r.DateGenerated, Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();
                    
                _logger.LogDebug("Retrieved {Count} recommendations for user: {UserId}", response.Models.Count, userId);
                return response.Models;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get recommendations for user: {UserId}", userId);
                return Enumerable.Empty<Recommendation>();
            }
        }

        public async Task<Recommendation?> GetRecommendationAsync(Guid recommendationId, string userId)
        {
            try
            {
                var userGuid = Guid.Parse(userId);
                var response = await _supabaseService.Client
                    .From<Recommendation>()
                    .Where(r => r.RecommendationId == recommendationId && r.UserId == userGuid)
                    .Single();
                    
                _logger.LogDebug("Retrieved recommendation: {RecommendationId} for user: {UserId}", recommendationId, userId);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get recommendation: {RecommendationId} for user: {UserId}", recommendationId, userId);
                return null;
            }
        }

        public async Task<Recommendation> CreateRecommendationAsync(RecommendationInsert recommendation)
        {
            try
            {
                var newRecommendation = new Recommendation
                {
                    RecommendationId = recommendation.RecommendationId ?? Guid.NewGuid(),
                    UserId = recommendation.UserId,
                    RecommendationText = recommendation.RecommendationText,
                    DateGenerated = recommendation.DateGenerated ?? DateTime.UtcNow
                };

                var response = await _supabaseService.Client
                    .From<Recommendation>()
                    .Upsert(newRecommendation);
                    
                _logger.LogInformation("Created recommendation for user: {UserId}", recommendation.UserId);
                return response.Models.First();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create recommendation for user: {UserId}", recommendation.UserId);
                throw;
            }
        }

        public async Task<Recommendation?> UpdateRecommendationAsync(Guid recommendationId, RecommendationUpdate recommendation, string userId)
        {
            try
            {
                var userGuid = Guid.Parse(userId);
                var existing = await GetRecommendationAsync(recommendationId, userId);
                if (existing == null) 
                {
                    _logger.LogWarning("Recommendation not found: {RecommendationId} for user: {UserId}", recommendationId, userId);
                    return null;
                }

                var response = await _supabaseService.Client
                    .From<Recommendation>()
                    .Where(r => r.RecommendationId == recommendationId && r.UserId == userGuid)
                    .Set(r => r.RecommendationText!, recommendation.RecommendationText!)
                    .Update();
                    
                _logger.LogInformation("Updated recommendation: {RecommendationId} for user: {UserId}", recommendationId, userId);
                return response.Models.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update recommendation: {RecommendationId} for user: {UserId}", recommendationId, userId);
                return null;
            }
        }

        public async Task<bool> DeleteRecommendationAsync(Guid recommendationId, string userId)
        {
            try
            {
                var userGuid = Guid.Parse(userId);
                await _supabaseService.Client
                    .From<Recommendation>()
                    .Where(r => r.RecommendationId == recommendationId && r.UserId == userGuid)
                    .Delete();
                    
                _logger.LogInformation("Deleted recommendation: {RecommendationId} for user: {UserId}", recommendationId, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete recommendation: {RecommendationId} for user: {UserId}", recommendationId, userId);
                return false;
            }
        }
    }
} 