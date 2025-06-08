using QiBalance.Components;
using QiBalance.Services;
using QiBalance.Models;
using Microsoft.AspNetCore.Components.Authorization;

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

            // Dodaj usługi Supabase
            builder.Services.AddSingleton<ISupabaseService, SupabaseService>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IDatabaseContext, DatabaseService>();
            
            // Dodaj dostawcę uwierzytelniania
            builder.Services.AddScoped<AuthenticationStateProvider, SupabaseAuthenticationStateProvider>();
            builder.Services.AddAuthorizationCore();

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
