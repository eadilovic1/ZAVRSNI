using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using ePinPong.Models;
using ePinPong.Services;

namespace ePinPong.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ResendEmailConfirmationModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMailQueueService _emailQueue;

        public ResendEmailConfirmationModel(UserManager<ApplicationUser> userManager, IMailQueueService emailQueue)
        {
            _userManager = userManager;
            _emailQueue = emailQueue;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public class InputModel
        {
            [Required(ErrorMessage = "Email adresa je obavezna.")]
            [EmailAddress(ErrorMessage = "Nevaljana email adresa.")]
            public string Email { get; set; } = string.Empty;
        }

        public void OnGet(string? email = null)
        {
            if (!string.IsNullOrEmpty(email))
            {
                Input.Email = email;
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                // Radi sigurnosti, ne otkrivamo eksplicitno da korisnik ne postoji
                return RedirectToPage("RegisterConfirmation", new { email = Input.Email });
            }

            if (await _userManager.IsEmailConfirmedAsync(user))
            {
                ModelState.AddModelError(string.Empty, "Ova email adresa je već potvrđena.");
                return Page();
            }

            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId = user.Id, code = code },
                protocol: Request.Scheme);

            _emailQueue.Enqueue(
                Input.Email,
                "ePinPong — Ponovno slanje potvrde email adrese",
                $"<div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px; max-width: 600px; margin: 0 auto;'>" +
                $"<h2 style='color: #0d6efd;'>ePinPong — Potvrda email adrese</h2>" +
                $"<p>Zatražili ste ponovno slanje linka za potvrdu email adrese. Kliknite na dugme ispod:</p>" +
                $"<p style='margin: 25px 0;'><a href='{HtmlEncoder.Default.Encode(callbackUrl!)}' style='background-color: #0d6efd; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;'>Potvrdi Email Adresu</a></p>" +
                $"<p style='color: #6c757d; font-size: 0.9em;'>Ako niste zatražili ovaj mail, možete ga ignorisati.</p>" +
                $"</div>");

            return RedirectToPage("RegisterConfirmation", new { email = Input.Email });
        }
    }
}
