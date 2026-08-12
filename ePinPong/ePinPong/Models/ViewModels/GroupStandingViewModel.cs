namespace ePinPong.Models.ViewModels
{
    public class GroupStandingViewModel
    {
        public string PlayerId { get; set; } = string.Empty;
        public string ImePrezime { get; set; } = string.Empty;
        public bool IsGost { get; set; }
        public string Grad { get; set; } = string.Empty;
        public int Pobjede { get; set; }
        public int Porazi { get; set; }
        public int SetRazlika { get; set; }
        public int OsvojeniSetovi { get; set; }
    }
}
