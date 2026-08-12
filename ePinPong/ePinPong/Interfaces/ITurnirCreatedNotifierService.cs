using ePinPong.Models;

namespace ePinPong.Services
{
    /// <summary>
    /// Šalje obavještenja pratiocima organizatora kada kreira novi turnir.
    /// </summary>
    public interface ITurnirCreatedNotifierService
    {
        Task ObavijestiPratioceAsync(Turnir turnir, string organizatorName);
    }
}
