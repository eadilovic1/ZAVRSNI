using ePinPong.Models;
using System.Collections.Generic;

namespace ePinPong.Services
{
    public interface IBracketDrawService
    {
        List<Mec> GenerirajGrupe(Turnir turnir, List<string> igracIds, bool useQualityGrouping = false);
    }
}
