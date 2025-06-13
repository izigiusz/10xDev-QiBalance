using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.SemanticKernel;
using QiBalance.Components;
using QiBalance.Models;
using QiBalance.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers().AddNewtonsoftJson(options =>
{
    options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<UserSessionState>();
builder.Services.AddSingleton<UserContext>();
builder.Services.AddScoped<AuthenticationStateProvider, SupabaseAuthenticationStateProvider>();

// Core Services
builder.Services.AddScoped<IValidationService, ValidationService>();
builder.Services.AddScoped<ISupabaseService, SupabaseService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDatabaseContext, DatabaseService>();
builder.Services.AddScoped<IOpenAIService, OpenAIService>();
builder.Services.AddScoped<IDiagnosticService, DiagnosticService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();

var openAIKey = builder.Configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrEmpty(openAIKey))
{
    throw new InvalidOperationException("OpenAI API key is not set. Check appsettings.json (OpenAI:ApiKey) or environment variables (OpenAI__ApiKey or OPENAI_API_KEY).");
}

builder.Services.AddKernel()
    .AddOpenAIChatCompletion("gpt-4o-mini", openAIKey);

builder.Services.AddScoped(sp =>
{
    var supabaseUrl = builder.Configuration["Supabase:Url"] ?? Environment.GetEnvironmentVariable("SUPABASE_URL");
    if (string.IsNullOrEmpty(supabaseUrl))
        throw new InvalidOperationException("Supabase URL is not configured. Check appsettings.json (Supabase:Url) or environment variables (SUPABASE_URL or Supabase__Url).");

    var supabaseKey = builder.Configuration["Supabase:Key"] ?? Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY");
    if (string.IsNullOrEmpty(supabaseKey))
        throw new InvalidOperationException("Supabase Key is not configured. Check appsettings.json (Supabase:Key) or environment variables (SUPABASE_ANON_KEY or Supabase__Key).");

    return new Supabase.Client(supabaseUrl, supabaseKey);
});

builder.Services.AddAuthorizationCore();
builder.Services.AddMemoryCache();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
else
{
    // W trybie Development lepiej widzieć pełne błędy
    app.UseDeveloperExceptionPage();
}

// Dodaj globalną obsługę błędów
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Nieobsłużony błąd w aplikacji");
        
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Wystąpił błąd. Spróbuj ponownie.");
        }
    }
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();