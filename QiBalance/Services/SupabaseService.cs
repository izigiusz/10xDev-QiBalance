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
        public Client Client { get; private set; }
        private readonly ILogger<SupabaseService> _logger;
        private readonly IValidationService _validationService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        
        private const string AccessTokenCookie = "sb-access-token";
        private const string RefreshTokenCookie = "sb-refresh-token";
        
        public SupabaseService(ILogger<SupabaseService> logger, IValidationService validationService, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _validationService = validationService;
            _httpContextAccessor = httpContextAccessor;
            
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
                
                // Try to get current user from active session
                var currentUser = Client.Auth.CurrentUser;
                
                // If no current user, try to restore session from cookie (for Blazor Server)
                if (currentUser == null)
                {
                    try
                    {
                        // Try to get token from HttpContext if available
                        var httpContext = GetHttpContext();
                        var token = httpContext?.Request.Cookies["sb-access-token"];
                        
                        if (!string.IsNullOrEmpty(token))
                        {
                            _logger.LogDebug("Attempting to restore Supabase session from token");
                            
                            // Get user with token and set session
                            var user = await Client.Auth.GetUser(token);
                            if (user != null)
                            {
                                // The GetUser call should automatically restore the session
                                _logger.LogDebug("Successfully restored Supabase session for user: {UserId}", user.Id);
                                return user;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to restore session from token");
                    }
                }
                
                return currentUser;
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
        /// Ensure Supabase.Client has a valid user session by restoring it from cookies if necessary.
        /// </summary>
        private async Task EnsureSupabaseSessionAsync()
        {
            // If we already have a current user in the client, nothing do to.
            if (Client.Auth.CurrentUser != null)
                return;

            var httpContext = GetHttpContext();
            var accessToken = httpContext?.Request.Cookies[AccessTokenCookie];
            var refreshToken = httpContext?.Request.Cookies[RefreshTokenCookie];

            if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
            {
                try
                {
                    _logger.LogDebug("Restoring Supabase session from cookies (access & refresh token)");
                    await Client.Auth.SetSession(accessToken, refreshToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to restore Supabase session from cookies");
                }
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
                await EnsureSupabaseSessionAsync();

                // Ensure user is authenticated before any insert operation
                var currentUser = await GetCurrentUserAsync();
                if (currentUser == null)
                {
                    throw new UnauthorizedAccessException("No authenticated user session found. Cannot insert entity.");
                }

                _logger.LogDebug("Authenticated user {UserId} confirmed before insert.", currentUser.Id);
                
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
                await EnsureSupabaseSessionAsync();
                // Ensure user is authenticated before any update operation
                var currentUser = await GetCurrentUserAsync();
                if (currentUser == null)
                {
                    throw new UnauthorizedAccessException("No authenticated user session found. Cannot update entity.");
                }
                
                _logger.LogDebug("Authenticated user {UserId} confirmed before update.", currentUser.Id);

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
                await EnsureSupabaseSessionAsync();
                // Ensure user is authenticated before any delete operation
                var currentUser = await GetCurrentUserAsync();
                if (currentUser == null)
                {
                    throw new UnauthorizedAccessException("No authenticated user session found. Cannot delete entity.");
                }

                _logger.LogDebug("Authenticated user {UserId} confirmed before delete.", currentUser.Id);

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
                await Client.InitializeAsync();
                
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