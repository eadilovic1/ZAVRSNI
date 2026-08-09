using ePinPong.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ePinPong.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            // 1. Seed Uloga (Roles)
            string[] uloge = { "Administrator", "Organizator", "Korisnik" };
            foreach (var uloga in uloge)
            {
                if (!await roleManager.RoleExistsAsync(uloga))
                {
                    await roleManager.CreateAsync(new IdentityRole(uloga));
                }
            }

            // 2. Seed Korisnika
            var adminUser = await userManager.FindByEmailAsync("admin@epinpong.com");
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = "admin@epinpong.com",
                    Email = "admin@epinpong.com",
                    EmailConfirmed = true,
                    Ime = "Admin",
                    Prezime = "Babo",
                    Grad = "Sarajevo",
                    DatumRodjenja = new DateTime(1990, 1, 1),
                    DatumRegistracije = DateTime.Now
                };
                var result = await userManager.CreateAsync(adminUser, "Admin007.");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Administrator");
                }
            }

            var orgUser = await userManager.FindByEmailAsync("organizator@epinpong.com");
            if (orgUser == null)
            {
                orgUser = new ApplicationUser
                {
                    UserName = "organizator@epinpong.com",
                    Email = "organizator@epinpong.com",
                    EmailConfirmed = true,
                    Ime = "Organizator",
                    Prezime = "Turnira",
                    Grad = "Tuzla",
                    DatumRodjenja = new DateTime(1985, 5, 12),
                    DatumRegistracije = DateTime.Now
                };
                var result = await userManager.CreateAsync(orgUser, "Admin007.");
                if (result.Succeeded)
                {
                    await userManager.AddToRolesAsync(orgUser, new[] { "Organizator", "Korisnik" });
                }
            }

            // Podrška za stare skripte koje koriste org@epinpong.com
            var legacyOrgUser = await userManager.FindByEmailAsync("org@epinpong.com");
            if (legacyOrgUser == null)
            {
                legacyOrgUser = new ApplicationUser
                {
                    UserName = "org@epinpong.com",
                    Email = "org@epinpong.com",
                    EmailConfirmed = true,
                    Ime = "Toni",
                    Prezime = "Kukoč",
                    Grad = "Split",
                    DatumRodjenja = new DateTime(1985, 5, 12),
                    DatumRegistracije = DateTime.Now
                };
                await userManager.CreateAsync(legacyOrgUser, "Admin007.");
                await userManager.AddToRolesAsync(legacyOrgUser, new[] { "Organizator", "Korisnik" });
            }

            var defaultIgrac = await userManager.FindByEmailAsync("igrac@epinpong.com");
            if (defaultIgrac == null)
            {
                defaultIgrac = new ApplicationUser
                {
                    UserName = "igrac@epinpong.com",
                    Email = "igrac@epinpong.com",
                    EmailConfirmed = true,
                    Ime = "Igrač",
                    Prezime = "Pro",
                    Grad = "Sarajevo",
                    DatumRodjenja = new DateTime(1995, 2, 2),
                    DatumRegistracije = DateTime.Now
                };
                var result = await userManager.CreateAsync(defaultIgrac, "Admin007.");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(defaultIgrac, "Korisnik");
                }
            }

            await EnsureSlobodanUserExistsAsync(context);
        }

        public static async Task EnsureSlobodanUserExistsAsync(ApplicationDbContext context)
        {
            if (!await context.Users.AnyAsync(u => u.Id == "SLOBODAN"))
            {
                var slobodanUser = new ApplicationUser
                {
                    Id = "SLOBODAN",
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
