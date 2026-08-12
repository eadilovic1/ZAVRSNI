using ePinPong.Models;

namespace ePinPong.Services
{
    /// <summary>
    /// Servis za primjenu šešira (pot assignment) na registracije turnira.
    /// </summary>
    public interface ISesiranjeService
    {
        /// <summary>
        /// Parsira playerPotsJson i primjenjuje šešire na registracije turnira.
        /// Vraca true ako je operacija uspjela, false ako parsiranje nije uspjelo.
        /// </summary>
        Task<bool> PrimijeniSesireAsync(Turnir turnir, string playerPotsJson);
    }
}
