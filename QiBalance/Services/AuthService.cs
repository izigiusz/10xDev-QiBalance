using Supabase.Gotrue;
using QiBalance.Services;

namespace QiBalance.Services
{
    public interface IAuthService
    {
        Task<Session?> SignInAsync(string email, string password);
        Task<Session?> SignUpAsync(string email, string password);
        Task SignOutAsync();
        Task<User?> GetCurrentUserAsync();
        bool IsAuthenticated { get; }
        event Action? AuthenticationStateChanged;
    }

    public class AuthService : IAuthService
    {
        private readonly ISupabaseService _supabaseService;
        private readonly ILogger<AuthService> _logger;
        
        public event Action? AuthenticationStateChanged;
        
        public AuthService(ISupabaseService supabaseService, ILogger<AuthService> logger)
        {
            _supabaseService = supabaseService;
            _logger = logger;
            
            // Subscribe to auth state changes
            _supabaseService.Client.Auth.AddStateChangedListener((sender, changed) =>
            {
                _logger.LogDebug("Auth state changed: {State}", changed);
                AuthenticationStateChanged?.Invoke();
            });
        }

        public bool IsAuthenticated => _supabaseService.Client.Auth.CurrentUser != null;

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
    }
} 