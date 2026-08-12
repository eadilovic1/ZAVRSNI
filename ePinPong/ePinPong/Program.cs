using ePinPong.Data;
using ePinPong.Models;
using ePinPong.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Globalization;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// 1. Konfiguracija baze podataka (SQLite za brz i samostalan prototip!)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// 2. Konfiguracija ASP.NET Identity
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// 3. Konfiguracija aplikacijskih cookia za login/logout rute
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// 4. Registracija Servisa i Interfejsa (SOLID principi!)
builder.Services.AddScoped<IMailService, MailService>();
builder.Services.AddScoped<IBracketDrawService, BracketDrawService>();
builder.Services.AddScoped<IBracketProgressionService, BracketProgressionService>();
builder.Services.AddScoped<IStandingsCalculationService, StandingsCalculationService>();
builder.Services.AddScoped<IBracketService, BracketService>();
builder.Services.AddScoped<ILeagueStandingsService, LeagueStandingsService>();
builder.Services.AddScoped<IMastersRegistrationService, MastersRegistrationService>();
builder.Services.AddScoped<ITurnirCompletionService, TurnirCompletionService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ISesiranjeService, SesiranjeService>();
builder.Services.AddScoped<ITurnirCreatedNotifierService, TurnirCreatedNotifierService>();

// Antiforgery konfiguracija za AJAX/fetch pozive
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

// Policy-based authorization i resource handler
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OrganizatorIliAdmin", policy =>
        policy.Requirements.Add(new ePinPong.Authorization.OrganizatorIliAdminRequirement()));
});
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, ePinPong.Authorization.TurnirOrganizatorHandler>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, ePinPong.Authorization.LigaOrganizatorHandler>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, ePinPong.Authorization.MecOrganizatorHandler>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages(); // Potrebno za Identity Razor Pages!

var app = builder.Build();

// 5. Pokretanje automatskog Seeda baze podataka na startu aplikacije
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        // Primijeni sve neprimijenjene EF Core migracije (kreira bazu ako ne postoji)
        await context.Database.MigrateAsync();

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        var configuration = services.GetRequiredService<IConfiguration>();

        // Asinhroni seeding podataka
        await DbSeeder.SeedAsync(context, userManager, roleManager, configuration);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Došlo je do greške prilikom migracije/seeding-a baze podataka.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// 7. Postavljanje kulture hr-HR kako bi se datumi prikazivali u formatu dd.MM.yyyy
var hrCulture = new CultureInfo("hr-HR");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(hrCulture),
    SupportedCultures = new[] { hrCulture },
    SupportedUICultures = new[] { hrCulture }
});

// 6. Rukovanje 404 i ostalim HTTP greškama (ErrorController!)
app.UseStatusCodePagesWithReExecute("/Error/{0}");

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages(); // Mapira Identity Razor Pages!

app.Run();
