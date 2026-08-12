using ePinPong.Models;
using System.Threading.Tasks;

namespace ePinPong.Services
{
    public interface IMastersRegistrationService
    {
        Task AutoRegistrirajIgraceLigeAsync(Liga liga, int turnirId);
    }
}