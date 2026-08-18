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

        /// <summary>
        /// ID pobjednika meča, ili null ako meč nije odigran ili nema oba igrača postavljena.
        /// Enkapsulira pravilo "veći broj setova pobjeđuje" na jednom mjestu.
        /// </summary>
        public string? PobjednikId => (Odigran && Igrac1ID != null && Igrac2ID != null)
            ? ((PoeniIgrac1 ?? 0) > (PoeniIgrac2 ?? 0) ? Igrac1ID : Igrac2ID)
            : null;

        /// <summary>ID gubitnika meča, ili null pod istim uslovima kao PobjednikId.</summary>
        public string? GubitnikId => (Odigran && Igrac1ID != null && Igrac2ID != null)
            ? ((PoeniIgrac1 ?? 0) > (PoeniIgrac2 ?? 0) ? Igrac2ID : Igrac1ID)
            : null;

        /// <summary>
        /// Provjera da li je rezultat meča validan prema pravilima stonog tenisa.
        /// Pobjednik mora imati tačno <see cref="SETOVA_ZA_POBJEDU"/> setova,
        /// a gubitnik između 0 i <see cref="SETOVA_ZA_POBJEDU"/> - 1 setova.
        /// </summary>
        /// <param name="poeniIgrac1">Broj setova koje je osvojio igrač 1.</param>
        /// <param name="poeniIgrac2">Broj setova koje je osvojio igrač 2.</param>
        /// <returns>True ako je rezultat validan, inače false.</returns>
        public static bool JeValidanRezultat(int poeniIgrac1, int poeniIgrac2)
        {
            bool igrac1Pobijedio = poeniIgrac1 == SETOVA_ZA_POBJEDU
                                   && poeniIgrac2 >= 0
                                   && poeniIgrac2 <= SETOVA_ZA_POBJEDU - 1;

            bool igrac2Pobijedio = poeniIgrac2 == SETOVA_ZA_POBJEDU
                                   && poeniIgrac1 >= 0
                                   && poeniIgrac1 <= SETOVA_ZA_POBJEDU - 1;

            return igrac1Pobijedio || igrac2Pobijedio;
        }

        // Novi atributi za napredni turnirski sistem
        public TipMeca TipMeca { get; set; } = TipMeca.GrupnaFaza;
        public string MatchCode { get; set; } = string.Empty;
        public MecKodovi? MecKodovi { get; set; }
        public string PlacingRange { get; set; } = string.Empty;
        public string? NazivGrupe { get; set; }
    }
}
