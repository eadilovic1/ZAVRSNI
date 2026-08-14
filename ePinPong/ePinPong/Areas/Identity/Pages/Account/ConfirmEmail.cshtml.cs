using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using ePinPong.Models;

namespace ePinPong.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ConfirmEmailModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ConfirmEmailModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public string StatusMessage { get; set; } = string.Empty;
        public bool Uspjesno { get; set; }

        public async Task<IActionResult> OnGetAsync(string userId, string code)
        {
            if (userId == null || code == null)
            {
                return RedirectToPage("/Index");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                StatusMessage = "Korisnik nije pronađen.";
                Uspjesno = false;
                return Page();
            }

            try
            {
                var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
                var result = await _userManager.ConfirmEmailAsync(user, decodedCode);

                if (result.Succeeded)
                {
                    StatusMessage = "Vaša email adresa je uspješno potvrđena. Sada se možete prijaviti na svoj račun.";
                    Uspjesno = true;
                }
                else
                {
                    StatusMessage = "Došlo je do greške prilikom potvrde email adrese. Link je možda istekao ili je već iskorišten.";
                    Uspjesno = false;
                }
            }
            catch
            {
                StatusMessage = "Nevažeći kod ili token za potvrdu email adrese.";
                Uspjesno = false;
            }

            return Page();
        }
    }
}
