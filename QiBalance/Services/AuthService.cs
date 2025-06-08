using Supabase.Gotrue;
using QiBalance.Services;
using QiBalance.Models.DTOs;
using System.ComponentModel.DataAnnotations;

namespace QiBalance.Services
{
    /// <summary>
    /// Interface for authentication service with user context management and RLS integration
    /// </summary>
    public interface IAuthService
    {
        // Existing authentication methods
        Task<Session?> SignInAsync(string email, string password);
        Task<Session?> SignUpAsync(string email, string password);
        Task SignOutAsync();
        Task<User?> GetCurrentUserAsync();
        bool IsAuthenticated { get; }
        event Action? AuthenticationStateChanged;

        // New methods for plan implementation
        Task<AuthResult> ValidateSessionAsync(string accessToken);
        Task<string?> GetCurrentUserEmailAsync();
        Task SetUserContextAsync(string userEmail);
        Task ClearUserContextAsync();
        string? CurrentUserEmail { get; }
    }

    /// <summary>
    /// Service providing authentication functionality with user context management and RLS integration
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly ISupabaseService _supabaseService;
        private readonly ILogger<AuthService> _logger;
        private readonly UserContext _userContext;
        private readonly IValidationService _validationService;
        
        public event Action? AuthenticationStateChanged;
        
        public AuthService(ISupabaseService supabaseService, ILogger<AuthService> logger, 
                          UserContext userContext, IValidationService validationService)
        {
            _supabaseService = supabaseService;
            _logger = logger;
            _userContext = userContext;
            _validationService = validationService;
            
            // Subscribe to auth state changes
            _supabaseService.Client.Auth.AddStateChangedListener((sender, changed) =>
            {
                _logger.LogDebug("Auth state changed: {State}", changed);
                AuthenticationStateChanged?.Invoke();
            });
        }

        public bool IsAuthenticated => _userContext.IsAuthenticated;
        public string? CurrentUserEmail => _userContext.Email;

        public async Task<Session?> SignInAsync(string email, string password)
        {
            try
            {
                var result = await _supabaseService.Client.Auth.SignIn(email, password);
                _logger.LogInformation("User signed in successfully: {Email}", email);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sign in user: {Email}", email);
                return null;
            }
        }

        public async Task<Session?> SignUpAsync(string email, string password)
        {
            try
            {
                var result = await _supabaseService.Client.Auth.SignUp(email, password);
                _logger.LogInformation("User signed up successfully: {Email}", email);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sign up user: {Email}", email);
                return null;
            }
        }

        public async Task SignOutAsync()
        {
            try
            {
                await _supabaseService.Client.Auth.SignOut();
                _logger.LogInformation("User signed out successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sign out user");
            }
        }

        public async Task<User?> GetCurrentUserAsync()
        {
            return await _supabaseService.GetCurrentUserAsync();
        }

        /// <summary>
        /// Validate session token and set user context
        /// </summary>
        public async Task<AuthResult> ValidateSessionAsync(string accessToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    return new AuthResult { Success = false, Error = "Access token is required" };
                }

                var user = await _supabaseService.Client.Auth.GetUser(accessToken);
                if (user?.Email != null)
                {
                    await SetUserContextAsync(user.Email);
                    return new AuthResult { Success = true, UserEmail = user.Email };
                }

                return new AuthResult { Success = false, Error = "Invalid access token" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating session token");
                return new AuthResult { Success = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Get current user email from context
        /// </summary>
        public async Task<string?> GetCurrentUserEmailAsync()
        {
            return await Task.FromResult(_userContext.Email);
        }

        /// <summary>
        /// Set user context and configure RLS
        /// </summary>
        public async Task SetUserContextAsync(string userEmail)
        {
            try
            {
                _validationService.ValidateUserId(userEmail);
                
                // Set user context
                _userContext.SetUser(userEmail);
                
                // Configure RLS in Supabase
                await _supabaseService.SetUserContextAsync(userEmail);
                
                _logger.LogInformation("User context set successfully: {UserEmail}", userEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting user context: {UserEmail}", userEmail);
                throw;
            }
        }

        /// <summary>
        /// Clear user context and RLS configuration
        /// </summary>
        public async Task ClearUserContextAsync()
        {
            try
            {
                var userEmail = _userContext.Email;
                
                // Clear user context
                _userContext.ClearUser();
                
                // Clear RLS in Supabase
                await _supabaseService.ClearUserContextAsync();
                
                _logger.LogInformation("User context cleared successfully for: {UserEmail}", userEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing user context");
                throw;
            }
        }
    }
} 