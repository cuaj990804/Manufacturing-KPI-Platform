using GDIKPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GDIKPI.Filters
{
    public class AreaAuthorizationFilter : IAsyncAuthorizationFilter
    {
        private readonly PermissionService _permissionService;
        private readonly string _requiredPermission;

        public AreaAuthorizationFilter(PermissionService permissionService, string requiredPermission)
        {
            _permissionService = permissionService;
            _requiredPermission = requiredPermission;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var employeeNumberClaim = context.HttpContext.User.FindFirst("EmployeeNumber")?.Value;

            if (string.IsNullOrEmpty(employeeNumberClaim))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            if (await _permissionService.IsUserAdmin(employeeNumberClaim))
            {
                return; // Admin tiene acceso a todo
            }

            if (!context.RouteData.Values.TryGetValue("areaId", out var areaIdValue) ||
                !int.TryParse(areaIdValue?.ToString(), out int areaId))
            {
                context.Result = new BadRequestObjectResult("areaId is required");
                return;
            }

            bool hasAccess = await _permissionService.CanUserAccessArea(employeeNumberClaim, areaId, _requiredPermission);
            if (!hasAccess)
            {
                context.Result = new ForbidResult();
                return;
            }
        }
    }

    public class RequireAreaPermissionAttribute : TypeFilterAttribute
    {
        public RequireAreaPermissionAttribute(string permission)
            : base(typeof(AreaAuthorizationFilter))
        {
            Arguments = new object[] { permission };
        }
    }

}
