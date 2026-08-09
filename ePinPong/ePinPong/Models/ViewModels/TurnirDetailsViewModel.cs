using ePinPong.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;

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
    }
}
