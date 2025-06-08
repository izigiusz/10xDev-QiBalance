using Supabase;
using QiBalance.Models;

namespace QiBalance.Services
{
    public interface ISupabaseService
    {
        Client Client { get; }
        Task<bool> IsUserAuthenticatedAsync();
        Task<Supabase.Gotrue.User?> GetCurrentUserAsync();
    }

    public class SupabaseService : ISupabaseService
    {
        public Client Client { get; private set; }
        private readonly ILogger<SupabaseService> _logger;
        
        public SupabaseService(ILogger<SupabaseService> logger)
        {
            _logger = logger;
            
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
    }
} 