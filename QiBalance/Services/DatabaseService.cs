using QiBalance.Models;
using QiBalance.Services;

namespace QiBalance.Services
{
    [Supabase.Postgrest.Attributes.Table("profiles")]
    public class Profile : Supabase.Postgrest.Models.BaseModel
    {
        [Supabase.Postgrest.Attributes.PrimaryKey("id")]
        public Guid Id { get; set; }

        [Supabase.Postgrest.Attributes.Column("email")]
        public string? Email { get; set; }
    }

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
                
                // Log raw data from database for debugging
                foreach (var rec in response.Models.Take(3))
                {
                    _logger.LogInformation("DB Raw: RecommendationId={RecommendationId}, Id={Id}, UserId={UserId}", 
                        rec.RecommendationId, rec.Id, rec.UserId);
                }
                
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
                    .Filter("recommendation_id", Supabase.Postgrest.Constants.Operator.Equals, recommendationId.ToString())
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userGuid.ToString())
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

                await _supabaseService.Client
                    .From<Recommendation>()
                    .Insert(newRecommendation);
                    
                _logger.LogInformation("Created recommendation with ID {RecommendationId} for user: {UserId}", newRecommendation.RecommendationId, recommendation.UserId);
                return newRecommendation;
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
                    .Filter("recommendation_id", Supabase.Postgrest.Constants.Operator.Equals, recommendationId.ToString())
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userGuid.ToString())
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
                    .Filter("recommendation_id", Supabase.Postgrest.Constants.Operator.Equals, recommendationId.ToString())
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userGuid.ToString())
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

        public async Task<Guid?> GetUserIdByEmailAsync(string email)
        {
            try
            {
                // Since we don't have access to auth.users table from the client,
                // and profiles table doesn't exist, we'll need to handle this differently.
                // For now, we'll return null and let the calling code handle the fallback.
                
                _logger.LogWarning("Cannot retrieve user ID for email {Email} - auth.users table not accessible from client", email);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user id for email: {Email}", email);
                return null;
            }
        }
    }
} 