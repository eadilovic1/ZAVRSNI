using System.Collections.Generic;

namespace ePinPong.Models.ViewModels
{
    public class UserRoleViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string Ime { get; set; } = string.Empty;
        public string Prezime { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Grad { get; set; } = string.Empty;
        public List<string> Uloge { get; set; } = new List<string>();
        public bool IsGost { get; set; }
    }
}