using ePinPong.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

namespace ePinPong.Authorization
{
    public class OrganizatorIliAdminRequirement : IAuthorizationRequirement { }

    public class TurnirOrganizatorHandler : AuthorizationHandler<OrganizatorIliAdminRequirement, Turnir>
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public TurnirOrganizatorHandler(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
            OrganizatorIliAdminRequirement requirement, Turnir resource)
        {
            if (context.User.IsInRole("Administrator"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            var userId = _userManager.GetUserId(context.User);
            if (!string.IsNullOrEmpty(userId) && resource != null && resource.OrganizatorId == userId)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }

    public class LigaOrganizatorHandler : AuthorizationHandler<OrganizatorIliAdminRequirement, Liga>
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public LigaOrganizatorHandler(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
            OrganizatorIliAdminRequirement requirement, Liga resource)
        {
            if (context.User.IsInRole("Administrator"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            var userId = _userManager.GetUserId(context.User);
            if (!string.IsNullOrEmpty(userId) && resource != null && resource.OrganizatorId == userId)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }

    public class MecOrganizatorHandler : AuthorizationHandler<OrganizatorIliAdminRequirement, Mec>
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public MecOrganizatorHandler(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
            OrganizatorIliAdminRequirement requirement, Mec resource)
        {
            if (context.User.IsInRole("Administrator"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            var userId = _userManager.GetUserId(context.User);
            if (!string.IsNullOrEmpty(userId) && resource?.Turnir != null && resource.Turnir.OrganizatorId == userId)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
