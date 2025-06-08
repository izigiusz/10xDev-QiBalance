using QiBalance.Components;
using QiBalance.Services;
using QiBalance.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.SemanticKernel;

namespace QiBalance
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            // Core Services - Infrastruktura
            builder.Services.AddScoped<IValidationService, ValidationService>();
            builder.Services.AddScoped<ISupabaseService, SupabaseService>();
            
            // User Context Management
            builder.Services.AddScoped<UserContext>();
            
            // Authentication Services
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IDatabaseContext, DatabaseService>();
            
            // AI Services - Semantic Kernel Configuration
            var openAIKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrEmpty(openAIKey))
            {
                throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set");
            }
            
            builder.Services.AddKernel()
                .AddOpenAIChatCompletion("gpt-4", openAIKey);
            
            builder.Services.AddScoped<IOpenAIService, OpenAIService>();
            builder.Services.AddScoped<IDiagnosticService, DiagnosticService>();
            builder.Services.AddScoped<IRecommendationService, RecommendationService>();
            
            // Dodaj dostawcę uwierzytelniania
            builder.Services.AddScoped<AuthenticationStateProvider, SupabaseAuthenticationStateProvider>();
            builder.Services.AddAuthorizationCore();
            
            // Caching dla sesji diagnostycznych
            builder.Services.AddMemoryCache();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}
