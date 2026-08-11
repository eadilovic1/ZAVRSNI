using ePinPong.Models;
using ePinPong.Models.ViewModels;
using System.Collections.Generic;

namespace ePinPong.Services
{
    public interface IStandingsCalculationService
    {
        List<TurnirPlasmanViewModel> IzracunajPlasman(Turnir turnir);
        (int L, int R) ParsirajRange(string? range);
        Dictionary<string, List<GroupStandingRow>> IzracunajTabeleGrupa(Turnir turnir);
        Dictionary<string, List<PairStandingRow>> IzracunajTabeleGrupaParova(Turnir turnir);
    }
}
