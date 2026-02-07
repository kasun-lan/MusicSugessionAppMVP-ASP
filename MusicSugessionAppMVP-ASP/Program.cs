using Microsoft.Extensions.Options;
using MusicSugessionAppMVP_ASP.Models;
using MusicSugessionAppMVP_ASP.Persistance;
using MusicSugessionAppMVP_ASP.Services;

namespace MusicSugessionAppMVP_ASP
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // -----------------------------
            // Persistence (EF Core DbContext)
            // -----------------------------
            builder.Services.AddPersistence(
                builder.Configuration.GetConnectionString("DefaultConnection")
            );

            // Apple Music (Developer Token)
            // Prefer configuration (appsettings / env vars), fallback to current hardcoded values.
            var appleTeamId = builder.Configuration["AppleMusic:TeamId"] ?? "429QUQ6VGA";
            var appleKeyId = builder.Configuration["AppleMusic:KeyId"] ?? "5A6HD2Z35H";
            var applePrivateKeyPath = builder.Configuration["AppleMusic:PrivateKeyPath"] ?? "Keys/AuthKey_5A6HD2Z35H.p8";
            if (!Path.IsPathRooted(applePrivateKeyPath))
                applePrivateKeyPath = Path.Combine(builder.Environment.ContentRootPath, applePrivateKeyPath);

            builder.Services.AddSingleton(new AppleMusicTokenService(
                teamId: appleTeamId,
                keyId: appleKeyId,
                privateKeyPath: applePrivateKeyPath
            ));

            builder.Services.AddHttpClient<AppleMusicService>();


            builder.Services.Configure<EmailSettings>(
            builder.Configuration.GetSection("Email"));

            builder.Services.AddSingleton(sp =>
                sp.GetRequiredService<IOptions<EmailSettings>>().Value);

            builder.Services.AddScoped<IEmailService, SmtpEmailService>();

            builder.Services.AddHttpClient<ISoundCloudApiService, SoundCloudApiService>(client =>
            {
                // optional defaults
            });

            builder.Services.AddScoped<ISoundCloudApiService>(sp =>
            {
                var http = sp.GetRequiredService<IHttpClientFactory>()
                             .CreateClient();

                var config = sp.GetRequiredService<IConfiguration>();

                return new SoundCloudApiService(
                    http,
                    config["SoundCloud:ClientId"],
                    config["SoundCloud:ClientSecret"]);
            });




            // Add services to the container.
            builder.Services.AddControllersWithViews();

            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromHours(12);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });
            builder.Services.AddHttpContextAccessor();



            var app = builder.Build();

            // ============================================
            // TEMPORARY TEST CODE - Remove after testing
            // ============================================
            if (app.Environment.IsDevelopment())
            {
                using (var scope = app.Services.CreateScope())
                {
                    var soundCloudService = scope.ServiceProvider.GetRequiredService<ISoundCloudApiService>();
                    
                    // Test SearchTrackIdAsync
                    Console.WriteLine("Testing SearchTrackIdAsync...");
                    var testTrackName = "Linking Park - What I've Done"; // Change this to test different tracks
                    var trackId = soundCloudService.SearchTrackIdAsync(testTrackName).GetAwaiter().GetResult();
                    
                    if (trackId.HasValue)
                    {
                        Console.WriteLine($"✓ Found track ID: {trackId.Value} for '{testTrackName}'");
                    }
                    else
                    {
                        Console.WriteLine($"✗ No track found for '{testTrackName}'");
                    }
                }
            }
            // ============================================

            app.UseSession();


            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
