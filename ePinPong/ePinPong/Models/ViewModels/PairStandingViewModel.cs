using ePinPong.Models;

namespace ePinPong.Models.ViewModels
{
    /// <summary>
    /// Jedan red u tabeli grupnih standings-a turnira parova (dublova).
    /// Analogno GroupStandingRow koji se koristi za singl grupe.
    /// </summary>
    public class PairStandingRow
    {
        public string Igrac1Id { get; set; } = string.Empty;
        public string Igrac2Id { get; set; } = string.Empty;

        /// <summary>Navigacioni objekat prvog igraca para.</summary>
        public ApplicationUser? Igrac1 { get; set; }

        /// <summary>Navigacioni objekat drugog igraca para.</summary>
        public ApplicationUser? Igrac2 { get; set; }

        public int Pobjede { get; set; }
        public int Porazi { get; set; }
        public int SetRazlika { get; set; }
        public int OsvojeniSetovi { get; set; }
    }
}
