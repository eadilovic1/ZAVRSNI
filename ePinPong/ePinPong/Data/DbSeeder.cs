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

            // 2. Seed Korisnika
            await CreateSeedUserIfNotExistsAsync(
                userManager,
                "admin@epinpong.com",
                defaultPassword,
                "Admin",
                "Babo",
                "Sarajevo",
                new DateTime(1990, 1, 1),
                AppConstants.Roles.Administrator);

            await CreateSeedUserIfNotExistsAsync(
                userManager,
                "organizator@epinpong.com",
                defaultPassword,
                "Organizator",
                "Turnira",
                "Tuzla",
                new DateTime(1985, 5, 12),
                AppConstants.Roles.Organizator,
                AppConstants.Roles.Korisnik);

            // Podrška za stare skripte koje koriste org@epinpong.com
            await CreateSeedUserIfNotExistsAsync(
                userManager,
                "org@epinpong.com",
                defaultPassword,
                "Toni",
                "Kukoč",
                "Split",
                new DateTime(1985, 5, 12),
                AppConstants.Roles.Organizator,
                AppConstants.Roles.Korisnik);

            await CreateSeedUserIfNotExistsAsync(
                userManager,
                "igrac@epinpong.com",
                defaultPassword,
                "Igrač",
                "Pro",
                "Sarajevo",
                new DateTime(1995, 2, 2),
                AppConstants.Roles.Korisnik);

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
                    DatumRodjenja = datumRodjenja,
                    DatumRegistracije = DateTime.Now
                };
                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded && uloge != null && uloge.Length > 0)
                {
                    await userManager.AddToRolesAsync(user, uloge);
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
                    DatumRodjenja = DateTime.MinValue,
                    DatumRegistracije = DateTime.Now
                };
                context.Users.Add(slobodanUser);
                await context.SaveChangesAsync();
            }
        }
    }
}
