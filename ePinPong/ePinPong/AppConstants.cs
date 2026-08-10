namespace ePinPong
{
    /// <summary>
    /// Centralne konstante aplikacije — eliminišu magic strings i magic numbers.
    /// </summary>
    public static class AppConstants
    {
        /// <summary>
        /// Nazivi uloga (rola) korisnika. Koristi umjesto string literala "Administrator" itd.
        /// </summary>
        public static class Roles
        {
            public const string Administrator = "Administrator";
            public const string Organizator   = "Organizator";
            public const string Korisnik      = "Korisnik";

            /// <summary>Kombinovani string za [Authorize(Roles = ...)] atribute.</summary>
            public const string AdministratorOrOrganizator = Administrator + "," + Organizator;
        }

        /// <summary>
        /// Default URL slike koji se koristi kad korisnik ne unese vlastitu sliku turnira.
        /// </summary>
        public const string DefaultTurnirSlikaUrl =
            "https://images.unsplash.com/photo-1534158914592-062992fbe900?q=80&w=1200&auto=format&fit=crop";

        /// <summary>
        /// Prefiksi za MatchCode mečeva.
        /// </summary>
        public static class MatchCodePrefixes
        {
            public const string Zavrsnica = "Z_";
            public const string Placement = "PL_";
            public const string Utjesni = "UT_";
            public const string UtjesniRoundRobin = "UT_RR_";
            public const string UtjesniPlacement = "UT_PL_";
        }
    }
}
