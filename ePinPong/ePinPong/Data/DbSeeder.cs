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
            // Osiguraj da je baza kreirana
            await context.Database.EnsureCreatedAsync();

            // Osiguraj da kolona Sesir postoji u tabeli Registracije (SQLite workaround za prototip)
            var columnExists = false;
            try
            {
                using (var command = context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = "PRAGMA table_info(Registracije);";
                    if (command.Connection.State != System.Data.ConnectionState.Open)
                    {
                        await command.Connection.OpenAsync();
                    }
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var name = reader.GetValue(reader.GetOrdinal("name"))?.ToString();
                            if ("Sesir".Equals(name, StringComparison.OrdinalIgnoreCase))
                            {
                                columnExists = true;
                                break;
                            }
                        }
                    }
                }

                if (!columnExists)
                {
                    await context.Database.ExecuteSqlRawAsync("ALTER TABLE Registracije ADD COLUMN Sesir INTEGER NOT NULL DEFAULT 1;");
                }
            }
            catch
            {
                // Ignoriši ako dođe do greške pri čitanju šeme
            }

            // Osiguraj da tabela TurnirParovi postoji (SQLite workaround za prototip)
            try
            {
                using (var command = context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='TurnirParovi';";
                    if (command.Connection.State != System.Data.ConnectionState.Open)
                    {
                        await command.Connection.OpenAsync();
                    }
                    var tableName = await command.ExecuteScalarAsync();
                    if (tableName == null)
                    {
                        await context.Database.ExecuteSqlRawAsync(
                            @"CREATE TABLE TurnirParovi (
                                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                                TurnirID INTEGER NOT NULL,
                                Igrac1ID TEXT NOT NULL,
                                Igrac2ID TEXT NOT NULL,
                                DatumPrijave TEXT NOT NULL,
                                FOREIGN KEY (TurnirID) REFERENCES Turniri(ID) ON DELETE CASCADE,
                                FOREIGN KEY (Igrac1ID) REFERENCES AspNetUsers(Id) ON DELETE RESTRICT,
                                FOREIGN KEY (Igrac2ID) REFERENCES AspNetUsers(Id) ON DELETE RESTRICT
                            );"
                        );
                    }
                }
            }
            catch
            {
                // Ignoriši
            }

            // Osiguraj da kolone Igrac1PartnerID i Igrac2PartnerID postoje u tabeli Mecevi
            try
            {
                var p1Exists = false;
                var p2Exists = false;
                using (var command = context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = "PRAGMA table_info(Mecevi);";
                    if (command.Connection.State != System.Data.ConnectionState.Open)
                    {
                        await command.Connection.OpenAsync();
                    }
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var name = reader.GetValue(reader.GetOrdinal("name"))?.ToString();
                            if ("Igrac1PartnerID".Equals(name, StringComparison.OrdinalIgnoreCase)) p1Exists = true;
                            if ("Igrac2PartnerID".Equals(name, StringComparison.OrdinalIgnoreCase)) p2Exists = true;
                        }
                    }
                }

                if (!p1Exists)
                {
                    await context.Database.ExecuteSqlRawAsync("ALTER TABLE Mecevi ADD COLUMN Igrac1PartnerID TEXT;");
                }
                if (!p2Exists)
                {
                    await context.Database.ExecuteSqlRawAsync("ALTER TABLE Mecevi ADD COLUMN Igrac2PartnerID TEXT;");
                }
            }
            catch
            {
                // Ignoriši
            }

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

        }
    }
}
