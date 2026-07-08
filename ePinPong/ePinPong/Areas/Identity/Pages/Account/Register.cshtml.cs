using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using ePinPong.Models;

namespace ePinPong.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
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
            public DateTime DatumRodjenja { get; set; } = DateTime.Now.AddYears(-20);

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
                    DatumRodjenja = Input.DatumRodjenja,
                    DatumRegistracije = DateTime.Now
                };

                var result = await _userManager.CreateAsync(user, Input.Password);
                if (result.Succeeded)
                {
                    _logger.LogInformation("Korisnik je kreirao novi račun sa lozinkom.");

                    // Automatsko dodjeljivanje uloge Korisnik
                    await _userManager.AddToRoleAsync(user, "Korisnik");

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return LocalRedirect(returnUrl);
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, TranslateError(error.Description));
                }
            }

            // Ako smo došli ovdje, nešto je pošlo po zlu, ponovo prikaži formu
            return Page();
        }

        private string TranslateError(string description)
        {
            if (description.Contains("is already taken"))
                return "Email adresa je već u upotrebi.";
            if (description.Contains("Password requires"))
                return "Lozinka mora sadržavati barem jednu cifru (0-9).";
            return description;
        }
    }
}
