using ePinPong.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System;
using System.Threading.Tasks;

namespace ePinPong.Authorization
{
    public class GenericOrganizatorIliAdminHandler<T> : AuthorizationHandler<OrganizatorIliAdminRequirement, T>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly Func<T, string?> _getOrganizatorId;

        public GenericOrganizatorIliAdminHandler(UserManager<ApplicationUser> userManager, Func<T, string?> getOrganizatorId)
        {
            _userManager = userManager;
            _getOrganizatorId = getOrganizatorId;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
            OrganizatorIliAdminRequirement requirement, T resource)
        {
            if (context.User.IsInRole(AppConstants.Roles.Administrator))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            var userId = _userManager.GetUserId(context.User);
            if (!string.IsNullOrEmpty(userId) && resource != null && _getOrganizatorId(resource) == userId)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
