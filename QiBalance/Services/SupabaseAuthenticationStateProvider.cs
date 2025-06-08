using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using QiBalance.Services;

namespace QiBalance.Services
{
    public class SupabaseAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
    {
        private readonly IAuthService _authService;
        private readonly ILogger<SupabaseAuthenticationStateProvider> _logger;
        
        public SupabaseAuthenticationStateProvider(IAuthService authService, ILogger<SupabaseAuthenticationStateProvider> logger)
        {
            _authService = authService;
            _logger = logger;
            
            // Subscribe to auth state changes
            _authService.AuthenticationStateChanged += OnAuthenticationStateChanged;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                var user = await _authService.GetCurrentUserAsync();
                
                if (user != null)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id),
                        new Claim(ClaimTypes.Email, user.Email ?? ""),
                        new Claim("sub", user.Id)
                    };

                    // Add additional claims if available
                    if (user.UserMetadata != null)
                    {
                        if (user.UserMetadata.ContainsKey("full_name") && user.UserMetadata["full_name"] != null)
                        {
                            claims.Add(new Claim(ClaimTypes.Name, user.UserMetadata["full_name"].ToString()!));
                        }
                        
                        if (user.UserMetadata.ContainsKey("first_name") && user.UserMetadata["first_name"] != null)
                        {
                            claims.Add(new Claim(ClaimTypes.GivenName, user.UserMetadata["first_name"].ToString()!));
                        }
                        
                        if (user.UserMetadata.ContainsKey("last_name") && user.UserMetadata["last_name"] != null)
                        {
                            claims.Add(new Claim(ClaimTypes.Surname, user.UserMetadata["last_name"].ToString()!));
                        }
                    }

                    var identity = new ClaimsIdentity(claims, "supabase");
                    var principal = new ClaimsPrincipal(identity);
                    
                    _logger.LogDebug("User authenticated: {UserId}", user.Id);
                    return new AuthenticationState(principal);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting authentication state");
            }

            _logger.LogDebug("User not authenticated");
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        public void NotifyAuthenticationStateChanged()
        {
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        private void OnAuthenticationStateChanged()
        {
            NotifyAuthenticationStateChanged();
        }

        public void Dispose()
        {
            _authService.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        }
    }
} 