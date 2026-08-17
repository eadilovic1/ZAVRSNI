using System;

namespace ePinPong.Models
{
    public class Notifikacija
    {
        public int ID { get; set; }
        public string Sadrzaj { get; set; } = string.Empty;
        public DateTime DatumKreiranja { get; set; } = DateTime.UtcNow;
        public bool Procitana { get; set; } = false;

        public string KorisnikId { get; set; } = string.Empty;
        public virtual ApplicationUser? Korisnik { get; set; }
    }
}
