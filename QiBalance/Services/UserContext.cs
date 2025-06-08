using QiBalance.Models.DTOs;

namespace QiBalance.Services
{
    /// <summary>
    /// Scoped service for managing user context within Blazor session
    /// Provides centralized user state management and authentication status
    /// </summary>
    public class UserContext
    {
        private string? _email;
        private readonly ILogger<UserContext> _logger;

        public UserContext(ILogger<UserContext> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Current user's email address
        /// </summary>
        public string? Email 
        { 
            get => _email;
            set
            {
                if (_email != value)
                {
                    var wasAuthenticated = IsAuthenticated;
                    _email = value;
                    
                    _logger.LogDebug("User context changed from {OldEmail} to {NewEmail}", 
                        wasAuthenticated ? "[authenticated]" : "[anonymous]", 
                        IsAuthenticated ? "[authenticated]" : "[anonymous]");
                    
                    // Notify about authentication state change
                    OnAuthenticationStateChanged?.Invoke(IsAuthenticated);
                }
            }
        }

        /// <summary>
        /// Indicates if user is authenticated
        /// </summary>
        public bool IsAuthenticated => !string.IsNullOrEmpty(_email);

        /// <summary>
        /// Event fired when authentication state changes
        /// </summary>
        public event Action<bool>? OnAuthenticationStateChanged;

        /// <summary>
        /// Set user context with validation
        /// </summary>
        public void SetUser(string? email)
        {
            Email = email;
        }

        /// <summary>
        /// Clear user context (logout)
        /// </summary>
        public void ClearUser()
        {
            Email = null;
        }

        /// <summary>
        /// Get user context as DTO
        /// </summary>
        public UserContext GetUserContext()
        {
            return new UserContext(_logger)
            {
                Email = this.Email
            };
        }

        /// <summary>
        /// Validate if current user matches provided userId
        /// </summary>
        public bool ValidateUser(string? userId)
        {
            if (!IsAuthenticated || string.IsNullOrEmpty(userId))
                return false;

            return string.Equals(_email, userId, StringComparison.OrdinalIgnoreCase);
        }
    }
} 