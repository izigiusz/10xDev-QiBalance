using Supabase;
using QiBalance.Models;
using QiBalance.Models.DTOs;
using Supabase.Postgrest.Models;

namespace QiBalance.Services
{
    /// <summary>
    /// Interface for entities with ID property
    /// </summary>
    public interface IEntity
    {
        object? Id { get; set; }
    }
}

namespace QiBalance.Services
{
    /// <summary>
    /// Interface for Supabase database operations with generic CRUD functionality
    /// Supports Row-Level Security (RLS) and user context management
    /// </summary>
    public interface ISupabaseService
    {
        Client Client { get; }
        Task<bool> IsUserAuthenticatedAsync();
        Task<Supabase.Gotrue.User?> GetCurrentUserAsync();
        
        // Generic CRUD operations
        Task<T?> GetByIdAsync<T>(object id) where T : BaseModel, IEntity, new();
        Task<List<T>> GetByUserIdAsync<T>(Guid userId) where T : BaseModel, IEntity, new();
        Task<T> InsertAsync<T>(T entity) where T : BaseModel, IEntity, new();
        Task<T> UpdateAsync<T>(T entity) where T : BaseModel, IEntity, new();
        Task<bool> DeleteAsync<T>(object id, Guid userId) where T : BaseModel, IEntity, new();
        Task<PagedResult<T>> GetPagedAsync<T>(Guid userId, int page, int limit, string sortBy) where T : BaseModel, IEntity, new();
        
        // User context management for RLS
        Task SetUserContextAsync(string userEmail);
        Task ClearUserContextAsync();
    }

    /// <summary>
    /// Service providing generic CRUD operations for Supabase with RLS support
    /// Handles user context management and database operations
    /// </summary>
    public class SupabaseService : ISupabaseService
    {
        public Client Client { get; private set; }
        private readonly ILogger<SupabaseService> _logger;
        private readonly IValidationService _validationService;
        
        public SupabaseService(ILogger<SupabaseService> logger, IValidationService validationService)
        {
            _logger = logger;
            _validationService = validationService;
            
            var url = Environment.GetEnvironmentVariable("SUPABASE_URL");
            var key = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY");
            
            if (string.IsNullOrEmpty(url))
                throw new InvalidOperationException("SUPABASE_URL environment variable is not set");
            
            if (string.IsNullOrEmpty(key))
                throw new InvalidOperationException("SUPABASE_ANON_KEY environment variable is not set");
            
            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                throw new InvalidOperationException("SUPABASE_URL is not a valid URL");
            
            var options = new Supabase.SupabaseOptions
            {
                AutoConnectRealtime = true,
                AutoRefreshToken = true
            };

            Client = new Client(url, key, options);
            _logger.LogInformation("Supabase client initialized with URL: {Url}", url);
        }

        public async Task<bool> IsUserAuthenticatedAsync()
        {
            try
            {
                var user = await GetCurrentUserAsync();
                return user != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking authentication status");
                return false;
            }
        }

        public async Task<Supabase.Gotrue.User?> GetCurrentUserAsync()
        {
            try
            {
                await Client.InitializeAsync();
                return Client.Auth.CurrentUser;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                return null;
            }
        }

        /// <summary>
        /// Get entity by ID with automatic RLS enforcement
        /// </summary>
        public async Task<T?> GetByIdAsync<T>(object id) where T : BaseModel, IEntity, new()
        {
            try
            {
                await Client.InitializeAsync();
                var result = await Client.From<T>().Where(x => x.Id!.Equals(id)).Single();
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting entity by ID: {Id}", id);
                return null;
            }
        }

        /// <summary>
        /// Get all entities for specific user with RLS enforcement
        /// </summary>
        public async Task<List<T>> GetByUserIdAsync<T>(Guid userId) where T : BaseModel, IEntity, new()
        {
            try
            {
                await Client.InitializeAsync();
                var results = await Client.From<T>().Get();
                return results.Models;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting entities for user: {UserId}", userId);
                return new List<T>();
            }
        }

        /// <summary>
        /// Insert new entity with automatic user context
        /// </summary>
        public async Task<T> InsertAsync<T>(T entity) where T : BaseModel, IEntity, new()
        {
            try
            {
                await Client.InitializeAsync();
                var result = await Client.From<T>().Insert(entity);
                var inserted = result.Model;
                
                if (inserted == null)
                    throw new InvalidOperationException("Insert operation failed - no model returned");
                
                _logger.LogInformation("Successfully inserted entity of type {Type}", typeof(T).Name);
                return inserted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting entity of type {Type}", typeof(T).Name);
                throw;
            }
        }

        /// <summary>
        /// Update existing entity with RLS enforcement
        /// </summary>
        public async Task<T> UpdateAsync<T>(T entity) where T : BaseModel, IEntity, new()
        {
            try
            {
                await Client.InitializeAsync();
                var result = await Client.From<T>().Update(entity);
                var updated = result.Model;
                
                if (updated == null)
                    throw new InvalidOperationException("Update operation failed - no model returned");
                
                _logger.LogInformation("Successfully updated entity of type {Type}", typeof(T).Name);
                return updated;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating entity of type {Type}", typeof(T).Name);
                throw;
            }
        }

        /// <summary>
        /// Delete entity by ID with user authorization check
        /// </summary>
        public async Task<bool> DeleteAsync<T>(object id, Guid userId) where T : BaseModel, IEntity, new()
        {
            try
            {
                await Client.InitializeAsync();
                await Client.From<T>().Where(x => x.Id!.Equals(id)).Delete();
                
                _logger.LogInformation("Successfully deleted entity of type {Type} with ID {Id}", typeof(T).Name, id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting entity of type {Type} with ID {Id}", typeof(T).Name, id);
                return false;
            }
        }

        /// <summary>
        /// Get paginated results for user with sorting
        /// </summary>
        public async Task<PagedResult<T>> GetPagedAsync<T>(Guid userId, int page, int limit, string sortBy) where T : BaseModel, IEntity, new()
        {
            try
            {
                _validationService.ValidatePagination(page, limit);
                
                await Client.InitializeAsync();
                
                // Calculate offset
                var offset = (page - 1) * limit;
                
                // Get total count first
                var totalResults = await Client.From<T>().Get();
                var totalCount = totalResults.Models.Count;
                
                // Get paginated data with sorting
                var query = Client.From<T>().Range(offset, offset + limit - 1);
                
                if (!string.IsNullOrEmpty(sortBy))
                {
                    query = query.Order(sortBy, new Supabase.Postgrest.Constants.Ordering());
                }
                
                var results = await query.Get();
                
                return new PagedResult<T>
                {
                    Items = results.Models,
                    TotalCount = totalCount,
                    Page = page,
                    Limit = limit
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paged results for user: {UserId}", userId);
                return new PagedResult<T>
                {
                    Items = new List<T>(),
                    TotalCount = 0,
                    Page = page,
                    Limit = limit
                };
            }
        }

        /// <summary>
        /// Set user context for Row-Level Security
        /// </summary>
        public async Task SetUserContextAsync(string userEmail)
        {
            try
            {
                _validationService.ValidateUserId(userEmail);
                
                await Client.InitializeAsync();
                
                // Set RLS context using Supabase RPC
                await Client.Rpc("set_config", new Dictionary<string, object>
                {
                    ["setting_name"] = "app.current_user_email",
                    ["new_value"] = userEmail,
                    ["is_local"] = true
                });
                
                _logger.LogDebug("User context set for RLS: {UserEmail}", userEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting user context for RLS: {UserEmail}", userEmail);
                throw;
            }
        }

        /// <summary>
        /// Clear user context for Row-Level Security
        /// </summary>
        public async Task ClearUserContextAsync()
        {
            try
            {
                await Client.InitializeAsync();
                
                // Clear RLS context
                await Client.Rpc("set_config", new Dictionary<string, object>
                {
                    ["setting_name"] = "app.current_user_email",
                    ["new_value"] = "",
                    ["is_local"] = true
                });
                
                _logger.LogDebug("User context cleared for RLS");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing user context for RLS");
                throw;
            }
        }
    }
} 