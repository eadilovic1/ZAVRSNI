using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using ePinPong;
using ePinPong.Models;
using ePinPong.Services;

namespace ePinPong.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IMailQueueService _emailQueue;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IMailQueueService emailQueue)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _emailQueue = emailQueue;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Ime je obavezno.")]
            [StringLength(50, ErrorMessage = "Ime ne može biti duže od 50 karaktera.")]
            [Display(Name = "Ime")]
            public string Ime { get; set; } = string.Empty;

            [Required(ErrorMessage = "Prezime je obavezno.")]
            [StringLength(50, ErrorMessage = "Prezime ne može biti duže od 50 karaktera.")]
            [Display(Name = "Prezime")]
            public string Prezime { get; set; } = string.Empty;

            [Required(ErrorMessage = "Grad je obavezan.")]
            [StringLength(50, ErrorMessage = "Naziv grada ne može biti duži od 50 karaktera.")]
            [Display(Name = "Grad")]
            public string Grad { get; set; } = string.Empty;

            [Required(ErrorMessage = "Datum rođenja je obavezan.")]
            [DataType(DataType.Date)]
            [Display(Name = "Datum Rođenja")]
            public DateTime DatumRodjenja { get; set; } = DateTime.UtcNow.AddYears(-20);

            [Required(ErrorMessage = "Email adresa je obavezna.")]
            [EmailAddress(ErrorMessage = "Nevaljana email adresa.")]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Lozinka je obavezna.")]
            [StringLength(100, ErrorMessage = "{0} mora biti barem {2} karaktera duga.", MinimumLength = 8)]
            [DataType(DataType.Password)]
            [Display(Name = "Lozinka")]
            public string Password { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Display(Name = "Potvrda Lozinke")]
            [Compare("Password", ErrorMessage = "Lozinke se ne podudaraju.")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public void OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = Input.Email,
                    Email = Input.Email,
                    Ime = Input.Ime,
                    Prezime = Input.Prezime,
                    Grad = Input.Grad,
                    DatumRodjenja = DateTime.SpecifyKind(Input.DatumRodjenja, DateTimeKind.Utc),
                    DatumRegistracije = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, Input.Password);
                if (result.Succeeded)
                {
                    _logger.LogInformation("Korisnik je kreirao novi račun sa lozinkom.");

                    // Automatsko dodjeljivanje uloge Korisnik
                    await _userManager.AddToRoleAsync(user, AppConstants.Roles.Korisnik);

                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = user.Id, code = code },
                        protocol: Request.Scheme);

                    _emailQueue.Enqueue(
                        Input.Email,
                        "ePinPong — Potvrda email adrese",
                        $"<div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px; max-width: 600px; margin: 0 auto;'>" +
                        $"<h2 style='color: #0d6efd;'>Dobrodošli na ePinPong!</h2>" +
                        $"<p>Hvala vam na registraciji. Molimo potvrdite svoju email adresu klikom na dugme ispod:</p>" +
                        $"<p style='margin: 25px 0;'><a href='{HtmlEncoder.Default.Encode(callbackUrl!)}' style='background-color: #0d6efd; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;'>Potvrdi Email Adresu</a></p>" +
                        $"<p style='color: #6c757d; font-size: 0.9em;'>Ako se niste registrovali na ePinPong platformi, slobodno zanemarite ovaj mail.</p>" +
                        $"</div>");

                    return RedirectToPage("RegisterConfirmation", new { email = Input.Email });
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, TranslateError(error));
                }
            }

            // Ako smo došli ovdje, nešto je pošlo po zlu, ponovo prikaži formu
            return Page();
        }

        private string TranslateError(IdentityError error)
        {
            if (error.Code == "DuplicateUserName" || error.Code == "DuplicateEmail")
                return "Email adresa je već u upotrebi.";
            if (error.Code == "PasswordRequiresDigit")
                return "Lozinka mora sadržavati barem jednu cifru (0-9).";
            return error.Description;
        }
    }
}
