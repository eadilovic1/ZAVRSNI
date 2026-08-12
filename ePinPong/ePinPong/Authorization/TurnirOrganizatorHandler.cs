using ePinPong.Models;
using Microsoft.AspNetCore.Identity;

namespace ePinPong.Authorization
{
    public class TurnirOrganizatorHandler : GenericOrganizatorIliAdminHandler<Turnir>
    {
        public TurnirOrganizatorHandler(UserManager<ApplicationUser> userManager)
            : base(userManager, t => t.OrganizatorId) { }
    }
}
