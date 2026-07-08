using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;

namespace ePinPong.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string Ime { get; set; } = string.Empty;
        public string Prezime { get; set; } = string.Empty;
        public string Grad { get; set; } = string.Empty;
        public DateTime DatumRodjenja { get; set; }
        public DateTime DatumRegistracije { get; set; } = DateTime.Now;

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public bool IsGost => string.IsNullOrEmpty(PasswordHash) || Email?.EndsWith("@epinpong.local") == true;

        // Relacije
        public virtual ICollection<Registracija> MojeRegistracije { get; set; } = new List<Registracija>();
        public virtual ICollection<Turnir> MojiTurniri { get; set; } = new List<Turnir>();
    }
}
