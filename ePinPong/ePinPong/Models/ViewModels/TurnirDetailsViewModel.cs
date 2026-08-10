using ePinPong.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ePinPong.Models.ViewModels
{
    public class TurnirDetailsViewModel
    {
        public Turnir Turnir { get; set; } = null!;
        public bool IsRegistered { get; set; }
        public bool IsOrganizator { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsMasters { get; set; }
        public string? CurrentUserId { get; set; }
        public List<TurnirPlasmanViewModel> Ranking { get; set; } = new();
        public Dictionary<string, int> PlayerPoints { get; set; } = new();
        public Dictionary<string, List<GroupStandingRow>> GroupStandings { get; set; } = new();
        public List<SelectListItem> SlobodniKorisnici { get; set; } = new();
        public List<SelectListItem> LigeZaSeeding { get; set; } = new();
        public List<SelectListItem> TurniriZaSeeding { get; set; } = new();

        // Forwarding svojstva radi 100% povratne kompatibilnosti u Razor view-u:
        public int ID => Turnir.ID;
        public string Naziv => Turnir.Naziv;
        public StatusTurnira Status => Turnir.Status;
        public TipTakmicenja TipTakmicenja => Turnir.TipTakmicenja;
        public SistemTurnira SistemTurnira => Turnir.SistemTurnira;
        public string? Opis => Turnir.Opis;
        public string Lokacija => Turnir.Lokacija;
        public string? SlikaUrl => Turnir.SlikaUrl;
        public DateTime DatumPocetka => Turnir.DatumPocetka;
        public int MaxIgraca => Turnir.MaxIgraca;
        public string OrganizatorId => Turnir.OrganizatorId;
        public ApplicationUser? Organizator => Turnir.Organizator;
        public int? LigaID => Turnir.LigaID;
        public Liga? Liga => Turnir.Liga;
        public ICollection<Mec> Mecevi => Turnir.Mecevi;
        public ICollection<Registracija> Registracije => Turnir.Registracije;
        public ICollection<TurnirPar> TurnirParovi => Turnir.TurnirParovi;

        // ===== Izvedene liste za Details.cshtml =====
        public List<Mec> GrupniMecevi =>
            Mecevi.Where(m => m.TipMeca == TipMeca.GrupnaFaza).ToList();

        public List<Mec> ZavrsniMecevi =>
            Mecevi.Where(m => m.TipMeca == TipMeca.Zavrsnica && m.MatchCode.StartsWith("Z_")).ToList();

        public List<Mec> RazigravanjeMecevi =>
            Mecevi.Where(m => m.TipMeca == TipMeca.Razigravanje && m.MatchCode.StartsWith("PL_")).ToList();

        public List<Mec> SviZavrsniMecevi =>
            Mecevi.Where(m => m.TipMeca == TipMeca.Zavrsnica).ToList();

        public List<Mec> UtjesniMecevi =>
            Mecevi.Where(m => m.TipMeca == TipMeca.Utjesni && m.MatchCode.StartsWith("UT_")).ToList();

        public List<Mec> UtjesniGlavniMecevi =>
            Mecevi.Where(m => m.TipMeca == TipMeca.Utjesni
                && m.MatchCode.StartsWith("UT_R") && !m.MatchCode.StartsWith("UT_RR_")).ToList();

        public List<Mec> UtjesniRazigravanjeMecevi =>
            Mecevi.Where(m => m.TipMeca == TipMeca.Utjesni
                && (m.MatchCode.StartsWith("UT_PL_") || m.MatchCode.StartsWith("UT_RR_"))).ToList();

        public int BrojGrupaUTurniru =>
            GrupniMecevi.Select(m => m.NazivGrupe).Where(n => !string.IsNullOrEmpty(n)).Distinct().Count();

        public bool IsGroupOnly =>
            GrupniMecevi.Any() && !SviZavrsniMecevi.Any() && !UtjesniMecevi.Any()
            && (BrojGrupaUTurniru == 1 || IsMasters);
    }
}
