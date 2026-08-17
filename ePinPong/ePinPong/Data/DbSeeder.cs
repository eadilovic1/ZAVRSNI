using ePinPong.Models;
using ePinPong.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ePinPong.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration? configuration = null)
        {
            string defaultPassword = configuration?["SeedData:DefaultPassword"] ?? "Admin007.";

            // 1. Seed Uloga (Roles)
            string[] uloge = { AppConstants.Roles.Administrator, AppConstants.Roles.Organizator, AppConstants.Roles.Korisnik };
            foreach (var uloga in uloge)
            {
                if (!await roleManager.RoleExistsAsync(uloga))
                {
                    await roleManager.CreateAsync(new IdentityRole(uloga));
                }
            }

            // 2. Postavljanje glavnog administratora (enis123.adilovic@gmail.com) sa svim ovlastima
            await CreateSeedUserIfNotExistsAsync(
                userManager,
                "enis123.adilovic@gmail.com",
                defaultPassword,
                "Enis",
                "Adilović",
                "Sarajevo",
                new DateTime(1995, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                AppConstants.Roles.Administrator,
                AppConstants.Roles.Organizator,
                AppConstants.Roles.Korisnik);

            // 3. Sistemski nalog za slobodne igrače u žrijebu (BYE)
            await EnsureSlobodanUserExistsAsync(context);
        }

        private static async Task CreateSeedUserIfNotExistsAsync(
            UserManager<ApplicationUser> userManager,
            string email,
            string password,
            string ime,
            string prezime,
            string grad,
            DateTime datumRodjenja,
            params string[] uloge)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    Ime = ime,
                    Prezime = prezime,
                    Grad = grad,
                    DatumRodjenja = datumRodjenja.Kind == DateTimeKind.Utc ? datumRodjenja : DateTime.SpecifyKind(datumRodjenja, DateTimeKind.Utc),
                    DatumRegistracije = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded && uloge != null && uloge.Length > 0)
                {
                    await userManager.AddToRolesAsync(user, uloge);
                }
            }
            else
            {
                user.EmailConfirmed = true;
                await userManager.UpdateAsync(user);
                if (uloge != null && uloge.Length > 0)
                {
                    foreach (var uloga in uloge)
                    {
                        if (!await userManager.IsInRoleAsync(user, uloga))
                        {
                            await userManager.AddToRoleAsync(user, uloga);
                        }
                    }
                }
            }
        }

        public static async Task EnsureSlobodanUserExistsAsync(ApplicationDbContext context)
        {
            if (!await context.Users.AnyAsync(u => u.Id == BracketService.SLOBODAN))
            {
                var slobodanUser = new ApplicationUser
                {
                    Id = BracketService.SLOBODAN,
                    UserName = "slobodan@epinpong.local",
                    NormalizedUserName = "SLOBODAN@EPINPONG.LOCAL",
                    Email = "slobodan@epinpong.local",
                    NormalizedEmail = "SLOBODAN@EPINPONG.LOCAL",
                    EmailConfirmed = true,
                    Ime = "Slobodan",
                    Prezime = "",
                    Grad = "N/A",
                    DatumRodjenja = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
                    DatumRegistracije = DateTime.UtcNow
                };
                context.Users.Add(slobodanUser);
                await context.SaveChangesAsync();
            }
        }
    }
}
