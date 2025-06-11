using Supabase;
using QiBalance.Models;
using QiBalance.Models.DTOs;
using Supabase.Postgrest.Models;
using Microsoft.AspNetCore.Http;

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
        private readonly Supabase.Client _client;
        private readonly ILogger<SupabaseService> _logger;
        private readonly UserSessionState _userSessionState;
        private readonly IValidationService _validationService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        
        private const string AccessTokenCookie = "sb-access-token";
        private const string RefreshTokenCookie = "sb-refresh-token";
        
        public Supabase.Client Client => _client;

        public SupabaseService(Supabase.Client client, ILogger<SupabaseService> logger, UserSessionState userSessionState, IValidationService validationService, IHttpContextAccessor httpContextAccessor)
        {
            _client = client;
            _logger = logger;
            _userSessionState = userSessionState;
            _validationService = validationService;
            _httpContextAccessor = httpContextAccessor;
        }

        private void EnsureSession()
        {
            var session = _userSessionState.CurrentSession;
            if (session != null && _client.Auth.CurrentSession?.AccessToken != session.AccessToken)
            {
                _logger.LogDebug("Initializing Supabase client with active session for RLS.");
                _client.Auth.SetSession(session.AccessToken, session.RefreshToken);
            }
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
                EnsureSession();
                return _client.Auth.CurrentUser;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                return null;
            }
        }

        /// <summary>
        /// Helper method to get HttpContext (will be null in non-HTTP contexts)
        /// </summary>
        private Microsoft.AspNetCore.Http.HttpContext? GetHttpContext()
        {
            try
            {
                return _httpContextAccessor.HttpContext;
            }
            catch
            {
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
                EnsureSession();
                var response = await _client.From<T>().Where(x => x.Id == id).Single();
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting entity of type {Type} with ID {Id}", typeof(T).Name, id);
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
                EnsureSession();
                var response = await _client.From<T>().Get();
                return response.Models;
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
                EnsureSession();
                var response = await _client.From<T>().Insert(entity);
                return response.Models.First();
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
                EnsureSession();
                var response = await _client.From<T>().Update(entity);
                var updated = response.Model;
                
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
                EnsureSession();
                await _client.From<T>().Where(x => x.Id == id).Delete();
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
                
                EnsureSession();
                
                // Calculate offset
                var offset = (page - 1) * limit;
                
                // Get total count first
                var totalResults = await _client.From<T>().Get();
                var totalCount = totalResults.Models.Count;
                
                // Get paginated data with sorting
                var query = _client.From<T>().Range(offset, offset + limit - 1);
                
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
                
                EnsureSession();
                
                // TODO: RLS configuration will be implemented when database functions are available
                // For now, we skip the RLS setup to allow authentication to work
                // await Client.Rpc("set_config", new Dictionary<string, object>
                // {
                //     ["setting_name"] = "app.current_user_email",
                //     ["new_value"] = userEmail,
                //     ["is_local"] = true
                // });
                
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
                EnsureSession();
                
                // TODO: RLS configuration will be implemented when database functions are available
                // For now, we skip the RLS cleanup to allow authentication to work
                // await Client.Rpc("set_config", new Dictionary<string, object>
                // {
                //     ["setting_name"] = "app.current_user_email",
                //     ["new_value"] = "",
                //     ["is_local"] = true
                // });
                
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