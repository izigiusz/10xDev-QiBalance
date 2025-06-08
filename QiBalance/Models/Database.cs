using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using QiBalance.Services;

namespace QiBalance.Models
{
    /// <summary>
    /// Database model for recommendations table
    /// Represents AI-generated health recommendations for authenticated users
    /// </summary>
    [Supabase.Postgrest.Attributes.Table("recommendations")]
    public class Recommendation : BaseModel, IEntity
    {
        /// <summary>
        /// Unique identifier for each recommendation
        /// </summary>
        [PrimaryKey("recommendation_id")]
        public Guid RecommendationId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Implementation of IEntity.Id property
        /// </summary>
        public object? Id 
        { 
            get => RecommendationId; 
            set => RecommendationId = value is Guid guid ? guid : Guid.Empty; 
        }

        /// <summary>
        /// UUID of the user who owns this recommendation (FK to auth.users.id)
        /// </summary>
        [Supabase.Postgrest.Attributes.Column("user_id")]
        public Guid UserId { get; set; }

        /// <summary>
        /// Timestamp when the recommendation was generated
        /// </summary>
        [Supabase.Postgrest.Attributes.Column("date_generated")]
        public DateTime DateGenerated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The actual recommendation content provided to the user
        /// </summary>
        [Supabase.Postgrest.Attributes.Column("recommendation_text")]
        public string RecommendationText { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for inserting new recommendations
    /// </summary>
    public class RecommendationInsert
    {
        public Guid? RecommendationId { get; set; }
        
        [Required]
        public Guid UserId { get; set; }
        
        public DateTime? DateGenerated { get; set; }
        
        [Required]
        public string RecommendationText { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for updating existing recommendations
    /// </summary>
    public class RecommendationUpdate
    {
        public Guid? RecommendationId { get; set; }
        public Guid? UserId { get; set; }
        public DateTime? DateGenerated { get; set; }
        public string? RecommendationText { get; set; }
    }

    /// <summary>
    /// Database context interface for dependency injection
    /// </summary>
    public interface IDatabaseContext
    {
        Task<IEnumerable<Recommendation>> GetRecommendationsAsync(Guid userId);
        Task<Recommendation?> GetRecommendationAsync(Guid recommendationId, Guid userId);
        Task<Recommendation> CreateRecommendationAsync(RecommendationInsert recommendation);
        Task<Recommendation?> UpdateRecommendationAsync(Guid recommendationId, RecommendationUpdate recommendation, Guid userId);
        Task<bool> DeleteRecommendationAsync(Guid recommendationId, Guid userId);
    }

    /// <summary>
    /// Constants for database schema
    /// </summary>
    public static class DatabaseConstants
    {
        public const string PublicSchema = "public";
        
        public static class Tables
        {
            public const string Recommendations = "recommendations";
        }
        
        public static class Columns
        {
            public static class Recommendations
            {
                public const string RecommendationId = "recommendation_id";
                public const string UserId = "user_id";
                public const string DateGenerated = "date_generated";
                public const string RecommendationText = "recommendation_text";
            }
        }
    }
}
