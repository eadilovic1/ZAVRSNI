using ePinPong.Models;
using Microsoft.AspNetCore.Identity;

namespace ePinPong.Authorization
{
    public class MecOrganizatorHandler : GenericOrganizatorIliAdminHandler<Mec>
    {
        public MecOrganizatorHandler(UserManager<ApplicationUser> userManager)
            : base(userManager, m => m.Turnir?.OrganizatorId) { }
    }
}
