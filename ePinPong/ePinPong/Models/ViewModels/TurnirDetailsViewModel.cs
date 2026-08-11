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
        public Dictionary<string, List<PairStandingRow>> PairGroupStandings { get; set; } = new();
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

        // ===== Izvedene liste za Details.cshtml (lazy-cached) =====
        private List<Mec>? _grupniMecevi;
        public List<Mec> GrupniMecevi =>
            _grupniMecevi ??= Mecevi.Where(m => m.TipMeca == TipMeca.GrupnaFaza).ToList();

        private List<Mec>? _zavrsniMecevi;
        public List<Mec> ZavrsniMecevi =>
            _zavrsniMecevi ??= Mecevi.Where(m => m.TipMeca == TipMeca.Zavrsnica && m.MatchCode.StartsWith(AppConstants.MatchCodePrefixes.Zavrsnica)).ToList();

        private List<Mec>? _razigravanjeMecevi;
        public List<Mec> RazigravanjeMecevi =>
            _razigravanjeMecevi ??= Mecevi.Where(m => m.TipMeca == TipMeca.Razigravanje && m.MatchCode.StartsWith(AppConstants.MatchCodePrefixes.Placement)).ToList();

        private List<Mec>? _sviZavrsniMecevi;
        public List<Mec> SviZavrsniMecevi =>
            _sviZavrsniMecevi ??= Mecevi.Where(m => m.TipMeca == TipMeca.Zavrsnica).ToList();

        private List<Mec>? _utjesniMecevi;
        public List<Mec> UtjesniMecevi =>
            _utjesniMecevi ??= Mecevi.Where(m => m.TipMeca == TipMeca.Utjesni && m.MatchCode.StartsWith(AppConstants.MatchCodePrefixes.Utjesni)).ToList();

        private List<Mec>? _utjesniGlavniMecevi;
        public List<Mec> UtjesniGlavniMecevi =>
            _utjesniGlavniMecevi ??= Mecevi.Where(m => m.TipMeca == TipMeca.Utjesni
                && m.MatchCode.StartsWith(AppConstants.MatchCodePrefixes.Utjesni + "R") && !m.MatchCode.StartsWith(AppConstants.MatchCodePrefixes.UtjesniRoundRobin)).ToList();

        private List<Mec>? _utjesniRazigravanjeMecevi;
        public List<Mec> UtjesniRazigravanjeMecevi =>
            _utjesniRazigravanjeMecevi ??= Mecevi.Where(m => m.TipMeca == TipMeca.Utjesni
                && (m.MatchCode.StartsWith(AppConstants.MatchCodePrefixes.UtjesniPlacement) || m.MatchCode.StartsWith(AppConstants.MatchCodePrefixes.UtjesniRoundRobin))).ToList();

        private int? _brojGrupaUTurniru;
        public int BrojGrupaUTurniru =>
            _brojGrupaUTurniru ??= GrupniMecevi.Select(m => m.NazivGrupe).Where(n => !string.IsNullOrEmpty(n)).Distinct().Count();

        private bool? _isGroupOnly;
        public bool IsGroupOnly =>
            _isGroupOnly ??= GrupniMecevi.Any() && !SviZavrsniMecevi.Any() && !UtjesniMecevi.Any()
            && (BrojGrupaUTurniru == 1 || IsMasters);
    }
}
