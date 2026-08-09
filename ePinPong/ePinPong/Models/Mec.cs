using System;

namespace ePinPong.Models
{
    public class Mec
    {
        /// <summary>Broj setova potrebnih za pobjedu u meču (domainsko pravilo).</summary>
        public const int SETOVA_ZA_POBJEDU = 3;

        public int ID { get; set; }
        
        public int TurnirID { get; set; }
        public virtual Turnir? Turnir { get; set; }

        public string? Igrac1ID { get; set; }
        public virtual ApplicationUser? Igrac1 { get; set; }

        public string? Igrac2ID { get; set; }
        public virtual ApplicationUser? Igrac2 { get; set; }

        public string? Igrac1PartnerID { get; set; }
        public virtual ApplicationUser? Igrac1Partner { get; set; }

        public string? Igrac2PartnerID { get; set; }
        public virtual ApplicationUser? Igrac2Partner { get; set; }

        public int? PoeniIgrac1 { get; set; }
        public int? PoeniIgrac2 { get; set; }
        
        public DateTime VrijemeMeca { get; set; }
        public int Runda { get; set; }
        public bool Odigran { get; set; } = false;

        // Novi atributi za napredni turnirski sistem
        public TipMeca TipMeca { get; set; } = TipMeca.GrupnaFaza;
        public string MatchCode { get; set; } = string.Empty;
        public string? WinnerNextMatchCode { get; set; }
        public string? LoserNextMatchCode { get; set; }
        public int? WinnerNextMatchSlot { get; set; }
        public int? LoserNextMatchSlot { get; set; }
        public string PlacingRange { get; set; } = string.Empty;
        public string? NazivGrupe { get; set; }
    }
}
