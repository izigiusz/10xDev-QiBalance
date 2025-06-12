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
            
            _authService.AuthenticationStateChanged += OnAuthenticationStateChanged;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                // Sprawdź, czy flaga wylogowania jest w cookie
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext?.Request.Cookies.ContainsKey("logout-in-progress") == true)
                {
                    _logger.LogInformation("Logout in progress detected, returning unauthenticated state.");
                    // Usuń flagę, aby nie blokowała przyszłych logowań
                    httpContext.Response.Cookies.Delete("logout-in-progress");
                    return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                }

                var user = await _authService.GetCurrentUserAsync();

                if (user == null)
                {
                    var token = httpContext?.Request.Cookies["sb-access-token"];
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