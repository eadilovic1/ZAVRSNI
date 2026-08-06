using ePinPong.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ePinPong.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Turnir> Turniri { get; set; }
        public DbSet<Mec> Mecevi { get; set; }
        public DbSet<Registracija> Registracije { get; set; }
        public DbSet<Notifikacija> Notifikacije { get; set; }
        public DbSet<Pracenje> Pracenja { get; set; }
        public DbSet<Liga> Lige { get; set; }
        public DbSet<TurnirPar> TurnirParovi { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Liga -> Organizator
            builder.Entity<Liga>()
                .HasOne(l => l.Organizator)
                .WithMany()
                .HasForeignKey(l => l.OrganizatorId)
                .OnDelete(DeleteBehavior.SetNull);

            // Turnir -> Liga
            builder.Entity<Turnir>()
                .HasOne(t => t.Liga)
                .WithMany(l => l.Turniri)
                .HasForeignKey(t => t.LigaID)
                .OnDelete(DeleteBehavior.Cascade);

            // Turnir -> Organizator
            builder.Entity<Turnir>()
                .HasOne(t => t.Organizator)
                .WithMany(u => u.MojiTurniri)
                .HasForeignKey(t => t.OrganizatorId)
                .OnDelete(DeleteBehavior.Cascade);

            // Registracija -> Turnir i Korisnik
            builder.Entity<Registracija>()
                .HasOne(r => r.Turnir)
                .WithMany(t => t.Registracije)
                .HasForeignKey(r => r.TurnirID)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Registracija>()
                .HasOne(r => r.Korisnik)
                .WithMany(u => u.MojeRegistracije)
                .HasForeignKey(r => r.KorisnikID)
                .OnDelete(DeleteBehavior.Cascade);

            // Mec -> Turnir
            builder.Entity<Mec>()
                .HasOne(m => m.Turnir)
                .WithMany(t => t.Mecevi)
                .HasForeignKey(m => m.TurnirID)
                .OnDelete(DeleteBehavior.Cascade);

            // Mec -> Igrac1 i Igrac2
            builder.Entity<Mec>()
                .HasOne(m => m.Igrac1)
                .WithMany()
                .HasForeignKey(m => m.Igrac1ID)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Mec>()
                .HasOne(m => m.Igrac2)
                .WithMany()
                .HasForeignKey(m => m.Igrac2ID)
                .OnDelete(DeleteBehavior.Restrict);

            // Pracenje -> Pratilac i Praceni
            builder.Entity<Pracenje>()
                .HasOne(p => p.Pratilac)
                .WithMany()
                .HasForeignKey(p => p.PratilacID)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Pracenje>()
                .HasOne(p => p.Praceni)
                .WithMany()
                .HasForeignKey(p => p.PraceniID)
                .OnDelete(DeleteBehavior.Cascade);

            // Notifikacija -> Korisnik
            builder.Entity<Notifikacija>()
                .HasOne(n => n.Korisnik)
                .WithMany()
                .HasForeignKey(n => n.KorisnikId)
                .OnDelete(DeleteBehavior.Cascade);

            // TurnirPar relationships
            builder.Entity<TurnirPar>()
                .HasOne(tp => tp.Turnir)
                .WithMany(t => t.TurnirParovi)
                .HasForeignKey(tp => tp.TurnirID)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<TurnirPar>()
                .HasOne(tp => tp.Igrac1)
                .WithMany()
                .HasForeignKey(tp => tp.Igrac1ID)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TurnirPar>()
                .HasOne(tp => tp.Igrac2)
                .WithMany()
                .HasForeignKey(tp => tp.Igrac2ID)
                .OnDelete(DeleteBehavior.Restrict);

            // Mec partner relationships
            builder.Entity<Mec>()
                .HasOne(m => m.Igrac1Partner)
                .WithMany()
                .HasForeignKey(m => m.Igrac1PartnerID)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Mec>()
                .HasOne(m => m.Igrac2Partner)
                .WithMany()
                .HasForeignKey(m => m.Igrac2PartnerID)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
