using ePinPong.Data;
using ePinPong.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ePinPong.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(
            string? searchQuery,
            StatusTurnira? statusId,
            string sortBy = "datum_novo")
        {
            var query = _context.Turniri
                .Include(t => t.Organizator)
                .Include(t => t.Liga)
                .Include(t => t.Registracije)
                .AsQueryable();

            // Pretraga po nazivu ili lokaciji
            if (!string.IsNullOrEmpty(searchQuery))
            {
                query = query.Where(t => t.Naziv.Contains(searchQuery) || t.Lokacija.Contains(searchQuery));
            }

            // Filtriranje po statusu
            if (statusId.HasValue)
            {
                query = query.Where(t => t.Status == statusId.Value);
            }

            // Sortiranje
            query = sortBy switch
            {
                "datum_novo" => query.OrderByDescending(t => t.DatumPocetka),
                "datum_staro" => query.OrderBy(t => t.DatumPocetka),
                "naziv_asc" => query.OrderBy(t => t.Naziv),
                "naziv_desc" => query.OrderByDescending(t => t.Naziv),
                "igraci_desc" => query.OrderByDescending(t => t.Registracije.Count),
                _ => query.OrderByDescending(t => t.DatumPocetka)
            };

            // Postavljanje ViewBag opcija za filtere (analogno ePazaru!)

            ViewBag.StatusOptions = Enum.GetValues(typeof(StatusTurnira))
                .Cast<StatusTurnira>()
                .Select(s => new SelectListItem
                {
                    Value = ((int)s).ToString(),
                    Text = s.ToString(),
                    Selected = statusId.HasValue && (int)statusId.Value == (int)s
                }).ToList();

            ViewBag.SortOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "datum_novo", Text = "Najnoviji turniri", Selected = sortBy == "datum_novo" },
                new SelectListItem { Value = "datum_staro", Text = "Stariji turniri", Selected = sortBy == "datum_staro" },
                new SelectListItem { Value = "naziv_asc", Text = "Naziv A-Z", Selected = sortBy == "naziv_asc" },
                new SelectListItem { Value = "naziv_desc", Text = "Naziv Z-A", Selected = sortBy == "naziv_desc" },
                new SelectListItem { Value = "igraci_desc", Text = "Najpopularniji", Selected = sortBy == "igraci_desc" }
            };

            ViewBag.CurrentSearch = searchQuery;
            ViewBag.CurrentStatus = statusId;
            ViewBag.CurrentSort = sortBy;

            var turniri = await query.ToListAsync();
            return View(turniri);
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}
