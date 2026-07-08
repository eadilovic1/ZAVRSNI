namespace ePinPong.Models
{
    public class Pracenje
    {
        public int ID { get; set; }

        public string PratilacID { get; set; } = string.Empty;
        public virtual ApplicationUser? Pratilac { get; set; }

        public string PraceniID { get; set; } = string.Empty;
        public virtual ApplicationUser? Praceni { get; set; }
    }
}
