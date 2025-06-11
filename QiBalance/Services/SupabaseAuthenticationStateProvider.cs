using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using QiBalance.Services;
using Microsoft.AspNetCore.Http;

namespace QiBalance.Services
{
    public class SupabaseAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
    {
        private readonly IAuthService _authService;
        private readonly ILogger<SupabaseAuthenticationStateProvider> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        
        public SupabaseAuthenticationStateProvider(IAuthService authService, ILogger<SupabaseAuthenticationStateProvider> logger, IHttpContextAccessor httpContextAccessor)
        {
            _authService = authService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            
            // Subscribe to auth state changes
            _authService.AuthenticationStateChanged += OnAuthenticationStateChanged;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                // Najpierw spróbuj z bieżącej sesji klienta Supabase
                var user = await _authService.GetCurrentUserAsync();

                // Jeśli nie ma, spróbuj odczytać token z cookie (dla nowego obwodu)
                if (user == null)
                {
                    var token = _httpContextAccessor.HttpContext?.Request.Cookies["sb-access-token"];
                    if (!string.IsNullOrEmpty(token))
                    {
                        _logger.LogInformation("Found auth token in cookie, attempting to restore session.");
                        user = await _authService.Client.Auth.GetUser(token);
                    }
                }
                
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