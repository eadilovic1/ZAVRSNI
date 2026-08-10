namespace ePinPong.Models.ViewModels
{
    /// <summary>
    /// ViewModel za postolje (podium) turnira parova.
    /// Koristi se u partial viewu _DoublesPodium.cshtml.
    /// </summary>
    public class DoublesPodiumViewModel
    {
        public ApplicationUser? Prvo_Igrac1  { get; set; }
        public ApplicationUser? Prvo_Igrac2  { get; set; }

        public ApplicationUser? Drugo_Igrac1 { get; set; }
        public ApplicationUser? Drugo_Igrac2 { get; set; }

        public List<(ApplicationUser? Igrac1, ApplicationUser? Igrac2)> TrecaMjesta { get; set; } = new();
    }
}
