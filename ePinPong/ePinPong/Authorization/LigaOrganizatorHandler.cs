using ePinPong.Models;
using Microsoft.AspNetCore.Identity;

namespace ePinPong.Authorization
{
    public class LigaOrganizatorHandler : GenericOrganizatorIliAdminHandler<Liga>
    {
        public LigaOrganizatorHandler(UserManager<ApplicationUser> userManager)
            : base(userManager, l => l.OrganizatorId) { }
    }
}
