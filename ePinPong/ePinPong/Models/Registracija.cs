using System;

namespace ePinPong.Models
{
    public class Registracija
    {
        public int ID { get; set; }

        public int TurnirID { get; set; }
        public virtual Turnir? Turnir { get; set; }

        public string KorisnikID { get; set; } = string.Empty;
        public virtual ApplicationUser? Korisnik { get; set; }

        public DateTime DatumRegistracije { get; set; } = DateTime.Now;
        public bool Odobren { get; set; } = false;

        public int Sesir { get; set; } = 1;
    }
}
