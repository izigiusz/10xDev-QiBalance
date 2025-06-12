using Supabase.Gotrue;
using QiBalance.Services;
using QiBalance.Models.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;


namespace QiBalance.Services
{
    /// <summary>
    /// Interface for authentication service with user context management and RLS integration
    /// </summary>
    public interface IAuthService
    {
        Supabase.Client Client { get; }
        // Existing authentication methods
        Task<Session?> SignInAsync(string email, string password);
        Task<Session?> SignUpAsync(string email, string password);
        Task SignOutAsync();
        
        bool IsAuthenticated { get; }
        event Action? AuthenticationStateChanged;

        // New methods for plan implementation
        Task<AuthResult> ValidateSessionAsync(string accessToken);
        Task<string?> GetCurrentUserEmailAsync();
        Task SetUserContextAsync(string userEmail);
        Task ClearUserContextAsync();
        string? CurrentUserEmail { get; }
        Task<string?> GetCurrentUserIdAsync();
        Task<Supabase.Gotrue.User?> GetCurrentUserAsync();
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
        private readonly NavigationManager _navigationManager;
        private readonly IJSRuntime _jsRuntime;
        private readonly Supabase.Client _client;
        private readonly UserSessionState _userSessionState;
        
        public event Action? AuthenticationStateChanged;
        
        public Supabase.Client Client => _client;

        public AuthService(ISupabaseService supabaseService, ILogger<AuthService> logger, 
                          UserContext userContext, IValidationService validationService,
                          NavigationManager navigationManager, IJSRuntime jsRuntime,
                          Supabase.Client client, UserSessionState userSessionState)
        {
            _supabaseService = supabaseService;
            _logger = logger;
            _userContext = userContext;
            _validationService = validationService;
            _navigationManager = navigationManager;
            _jsRuntime = jsRuntime;
            _client = client;
            _userSessionState = userSessionState;
            
            _client.Auth.AddStateChangedListener((sender, changed) =>
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
                var result = await _client.Auth.SignIn(email, password);
                if (result != null)
                {
                    // Set user context after successful login
                    await SetUserContextAsync(email);
                    _userSessionState.CurrentSession = result;
                    
                    // Powiadom o zmianie stanu uwierzytelniania
                    AuthenticationStateChanged?.Invoke();
                    
                    _logger.LogInformation("User signed in successfully: {Email}, Session saved: {HasSession}", 
                        email, _userSessionState.CurrentSession != null);
                }
                else
                {
                    _logger.LogWarning("Sign in failed for user: {Email} - no session returned", email);
                }
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
                var result = await _client.Auth.SignUp(email, password);
                if (result != null)
                {
                    // Set user context after successful registration
                    await SetUserContextAsync(email);
                    _userSessionState.CurrentSession = result;
                    _logger.LogInformation("User signed up successfully: {Email}, Session saved: {HasSession}", 
                        email, _userSessionState.CurrentSession != null);
                }
                else
                {
                    _logger.LogWarning("Sign up failed for user: {Email} - no session returned", email);
                }
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
                _logger.LogInformation("Signing out user.");
                await _client.Auth.SignOut();
                
                _userSessionState.CurrentSession = null;
                await ClearUserContextAsync();
                
                // Ustaw cookie flagę, aby poinformować o wylogowaniu po przeładowaniu
                await _jsRuntime.InvokeVoidAsync("eval", "document.cookie = 'logout-in-progress=true; path=/; max-age=10'");
                
                // Usuń cookie tokena
                await _jsRuntime.InvokeVoidAsync("eval", "document.cookie = 'sb-access-token=; path=/; max-age=0'");
                
                _navigationManager.NavigateTo("/", forceLoad: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sign out user");
                // Nadal spróbuj przekierować
                _navigationManager.NavigateTo("/", forceLoad: true);
            }
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

                var user = await _client.Auth.GetUser(accessToken);
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

        public async Task<string?> GetCurrentUserIdAsync()
        {
            var session = _userSessionState.CurrentSession ?? await _client.Auth.RetrieveSessionAsync();
            _userSessionState.CurrentSession = session;
            return session?.User?.Id;
        }

        public async Task<Supabase.Gotrue.User?> GetCurrentUserAsync()
        {
            try
            {
                _logger.LogInformation("Getting current user - checking session state...");
                
                // First try to get from our session state
                var session = _userSessionState.CurrentSession;
                if (session?.User != null)
                {
                    _logger.LogInformation("Retrieved user from session state: {UserId}", session.User.Id);
                    return session.User;
                }
                _logger.LogInformation("No user in session state, trying Supabase...");

                // Fallback: try to retrieve session from Supabase
                session = await _client.Auth.RetrieveSessionAsync();
                if (session?.User != null)
                {
                    _userSessionState.CurrentSession = session;
                    _logger.LogInformation("Retrieved and cached user from Supabase: {UserId}", session.User.Id);
                    return session.User;
                }
                _logger.LogInformation("No session from Supabase, trying SupabaseService...");

                // Final fallback: try the original method
                var user = await _supabaseService.GetCurrentUserAsync();
                if (user != null)
                {
                    _logger.LogInformation("Retrieved user from SupabaseService: {UserId}", user.Id);
                    return user;
                }

                _logger.LogWarning("No authenticated user found in any method");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                return null;
            }
        }

        private async Task<AuthResult> HandleAuthResponse(Func<Task<Session?>> authAction)
        {
            try
            {
                var result = await authAction();
                if (result?.User?.Email != null)
                {
                    // Set user context after successful login or registration
                    await SetUserContextAsync(result.User.Email);
                    _logger.LogInformation("User {Action} successfully: {Email}", authAction.Method.Name, result.User.Email);
                    return new AuthResult { Success = true, UserEmail = result.User.Email };
                }
                return new AuthResult { Success = false, Error = "Authentication failed" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error {Action}", authAction.Method.Name);
                return new AuthResult { Success = false, Error = ex.Message };
            }
        }
    }
} 