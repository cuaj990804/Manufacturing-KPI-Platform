using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using GDIKPI.Services;

namespace GDIKPI.Filters
{
    public class ProductionLineAuthorizationFilter : IAsyncAuthorizationFilter
    {
        private readonly PermissionService _permissionService;
        private readonly string _requiredPermission;

        public ProductionLineAuthorizationFilter(PermissionService permissionService, string requiredPermission)
        {
            _permissionService = permissionService;
            _requiredPermission = requiredPermission;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            // Obtener el número de empleado del usuario autenticado
            var employeeNumberClaim = context.HttpContext.User.FindFirst("EmployeeNumber")?.Value;

            if (string.IsNullOrEmpty(employeeNumberClaim))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Verificar si es administrador
            if (await _permissionService.IsUserAdmin(employeeNumberClaim))
            {
                return; // Admin tiene acceso a todo
            }

            // Obtener el ID de la línea de producción desde la ruta o query string
            var productionLineId = GetProductionLineId(context);

            if (productionLineId == null)
            {
                context.Result = new BadRequestObjectResult("ProductionLineId is required");
                return;
            }

            // Verificar permisos
            var hasAccess = await _permissionService.HasAccessToLine(employeeNumberClaim, productionLineId.Value, _requiredPermission);

            if (!hasAccess)
            {
                context.Result = new ForbidResult();
                return;
            }

            // Auditoría de acceso removida - solo se registrarán cambios en datos de producción
        }

        private int? GetProductionLineId(AuthorizationFilterContext context)
        {
            // Intentar obtener desde route values
            if (context.RouteData.Values.TryGetValue("productionLineId", out var routeValue))
            {
                if (int.TryParse(routeValue?.ToString(), out int id))
                    return id;
            }

            // Intentar obtener desde query string
            if (context.HttpContext.Request.Query.TryGetValue("productionLineId", out var queryValue))
            {
                if (int.TryParse(queryValue, out int id))
                    return id;
            }

            // Intentar obtener desde el body para POST/PUT requests
            if (context.HttpContext.Request.HasFormContentType)
            {
                if (context.HttpContext.Request.Form.TryGetValue("productionLineId", out var formValue))
                {
                    if (int.TryParse(formValue, out int id))
                        return id;
                }
            }

            return null;
        }
    }

    // Attribute para facilitar el uso
    public class RequireProductionLinePermissionAttribute : TypeFilterAttribute
    {
        public RequireProductionLinePermissionAttribute(string permission)
            : base(typeof(ProductionLineAuthorizationFilter))
        {
            Arguments = new object[] { permission };
        }
    }
}