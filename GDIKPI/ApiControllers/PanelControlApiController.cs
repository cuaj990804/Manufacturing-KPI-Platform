using GDIKPI.Data;
using GDIKPI.Models;
using GDIKPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GDIKPI.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "admin")]
    public class PanelControlController : ControllerBase
    {
        private readonly KpisContext _context;
        private readonly PermissionService _permissionService;

        public PanelControlController(KpisContext context, PermissionService permissionService)
        {
            _context = context;
            _permissionService = permissionService;
        }

        [HttpGet("GetAreas")]
        public async Task<IActionResult> GetAreas()
        {
            try
            {
                var areas = await _context.Areas
                    .Select(a => new
                    {
                        areaId = a.AreaId,
                        customerName = a.CustomerName,
                        areaName = a.AreaName
                    })
                    .ToListAsync();

                return Ok(areas);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al obtener áreas", error = ex.Message });
            }
        }

        [HttpPost("AddUser")]
        public async Task<IActionResult> AddUser([FromBody] AddUserRequest request)
        {
            try
            {
                // Verificar si el usuario ya existe
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.EmployeeNumber == request.EmployeeNumber.ToString());

                if (existingUser != null)
                {
                    return BadRequest(new { success = false, message = "El número de empleado ya existe" });
                }

                var newUser = new User
                {
                    EmployeeNumber = request.EmployeeNumber.ToString(),
                    Name = request.Name,
                    Role = request.Role,
                    Area = request.Area
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Usuario creado correctamente" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error al crear usuario", error = ex.Message });
            }
        }

        [HttpDelete("DeleteUser/{employeeNumber}")]
        public async Task<IActionResult> DeleteUser(string employeeNumber)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.EmployeeNumber == employeeNumber);

                if (user == null)
                {
                    return NotFound(new { success = false, message = "Usuario no encontrado" });
                }

                // Eliminar todos los permisos relacionados
                var modulePermissions = await _context.UserModulePermissions
                    .Where(ump => ump.EmployeeNumber == employeeNumber)
                    .ToListAsync();

                var areaPermissions = await _context.UserAreaPermissions
                    .Where(uap => uap.EmployeeNumber == employeeNumber)
                    .ToListAsync();

                var linePermissions = await _context.UserLinePermissions
                    .Where(ulp => ulp.EmployeeNumber == employeeNumber)
                    .ToListAsync();

                // Remover todos los permisos
                if (modulePermissions.Any())
                    _context.UserModulePermissions.RemoveRange(modulePermissions);

                if (areaPermissions.Any())
                    _context.UserAreaPermissions.RemoveRange(areaPermissions);

                if (linePermissions.Any())
                    _context.UserLinePermissions.RemoveRange(linePermissions);

                // Remover el usuario
                _context.Users.Remove(user);

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Usuario eliminado correctamente" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error al eliminar usuario", error = ex.Message, details = ex.InnerException?.Message });
            }
        }

        [HttpGet("GetUserAreaPermissions/{employeeNumber}")]
        public async Task<IActionResult> GetUserAreaPermissions(string employeeNumber)
        {
            try
            {
                var permissions = await _context.UserAreaPermissions
                    .Where(uap => uap.EmployeeNumber == employeeNumber)
                    .Select(uap => new
                    {
                        areaId = uap.AreaId,
                        permissionId = uap.PermissionId
                    })
                    .ToListAsync();

                return Ok(permissions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al obtener permisos de área", error = ex.Message });
            }
        }

        [HttpGet("GetUserLinePermissions/{employeeNumber}")]
        public async Task<IActionResult> GetUserLinePermissions(string employeeNumber)
        {
            try
            {
                var permissions = await _context.UserLinePermissions
                    .Where(ulp => ulp.EmployeeNumber == employeeNumber)
                    .Select(ulp => new
                    {
                        productionLinesId = ulp.ProductionLinesId,
                        permissionId = ulp.PermissionId
                    })
                    .ToListAsync();

                return Ok(permissions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al obtener permisos de línea", error = ex.Message });
            }
        }

        [HttpGet("GetAllAreas")]
        public async Task<IActionResult> GetAllAreas()
        {
            try
            {
                var areas = await _context.Areas
                    .Select(a => new
                    {
                        areaId = a.AreaId,
                        customerName = a.CustomerName,
                        areaName = a.AreaName
                    })
                    .ToListAsync();

                return Ok(areas);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al obtener áreas", error = ex.Message });
            }
        }

        [HttpGet("GetAllLines")]
        public async Task<IActionResult> GetAllLines()
        {
            try
            {
                var lines = await _context.ProductionLines
                    .Select(pl => new
                    {
                        productionLinesId = pl.ProductionLinesId,
                        lineNumber = pl.LineNumber,
                        areaId = pl.AreaId
                    })
                    .ToListAsync();

                return Ok(lines);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al obtener líneas", error = ex.Message });
            }
        }

        [HttpGet("GetAllPermissions")]
        public async Task<IActionResult> GetAllPermissions()
        {
            try
            {
                var permissions = await _context.Permissions
                    .Select(p => new
                    {
                        permissionId = p.PermissionId,
                        name = p.Name,
                        description = p.Description
                    })
                    .ToListAsync();

                return Ok(permissions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al obtener permisos", error = ex.Message });
            }
        }

        [HttpGet("GetModules")]
        public async Task<IActionResult> GetModules()
        {
            try
            {
                var modules = await _context.Modules
                    .Where(m => m.IsActive)
                    .Select(m => new
                    {
                        moduleId = m.ModuleId,
                        moduleName = m.ModuleName,
                        description = m.Description,
                        route = m.Route,
                        isActive = m.IsActive
                    })
                    .OrderBy(m => m.moduleName)
                    .ToListAsync();

                return Ok(modules);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al obtener módulos", error = ex.Message });
            }
        }

        [HttpGet("SearchUsers")]
        public async Task<IActionResult> SearchUsers([FromQuery] string search = "", [FromQuery] int limit = 20)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(search) || search.Length < 2)
                {
                    return Ok(new List<object>());
                }

                var query = _context.Users.AsQueryable();

                // Buscar por número de empleado o nombre
                query = query.Where(u =>
                    u.EmployeeNumber.Contains(search) ||
                    u.Name.Contains(search));

                var users = await query
                    .OrderBy(u => u.Name)
                    .Take(limit)
                    .Select(u => new
                    {
                        employeeNumber = u.EmployeeNumber,
                        name = u.Name
                    })
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al buscar usuarios", error = ex.Message });
            }
        }

        [HttpGet("GetUsersPaginated")]
        public async Task<IActionResult> GetUsersPaginated(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string search = "",
            [FromQuery] string area = "",
            [FromQuery] string role = "")
        {
            try
            {
                var query = _context.Users.AsQueryable();

                // Aplicar filtros
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(u =>
                        u.Name.Contains(search) ||
                        u.EmployeeNumber.Contains(search));
                }

                if (!string.IsNullOrWhiteSpace(area))
                {
                    query = query.Where(u => u.Area == area);
                }

                if (!string.IsNullOrWhiteSpace(role))
                {
                    query = query.Where(u => u.Role == role);
                }

                // Contar total de registros
                var totalRecords = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

                // Obtener usuarios paginados
                var users = await query
                    .OrderBy(u => u.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new
                    {
                        employeeNumber = u.EmployeeNumber,
                        name = u.Name,
                        role = u.Role,
                        area = u.Area
                    })
                    .ToListAsync();

                return Ok(new
                {
                    users = users,
                    pagination = new
                    {
                        currentPage = page,
                        pageSize = pageSize,
                        totalRecords = totalRecords,
                        totalPages = totalPages
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al obtener usuarios paginados", error = ex.Message });
            }
        }

        [HttpGet("GetUserModulePermissions/{employeeNumber}")]
        public async Task<IActionResult> GetUserModulePermissions(string employeeNumber)
        {
            try
            {
                var modulePermissions = await _context.UserModulePermissions
                    .Include(ump => ump.Module)
                    .Include(ump => ump.Permission)
                    .Where(ump => ump.EmployeeNumber == employeeNumber)
                    .Select(ump => new
                    {
                        moduleId = ump.ModuleId,
                        moduleName = ump.Module.ModuleName,
                        permissionId = ump.PermissionId,
                        permissionName = ump.Permission.Name,
                        isActive = ump.Module.IsActive
                    })
                    .ToListAsync();

                return Ok(modulePermissions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al obtener permisos de módulo", error = ex.Message });
            }
        }

        [HttpGet("GetUserModulePermissions/{employeeNumber}/{moduleId}")]
        public async Task<IActionResult> GetUserModulePermissions(string employeeNumber, int moduleId)
        {
            try
            {
                var modulePermissions = await _context.UserModulePermissions
                    .Where(ump => ump.EmployeeNumber == employeeNumber && ump.ModuleId == moduleId)
                    .Select(ump => new
                    {
                        permissionId = ump.PermissionId
                    })
                    .ToListAsync();

                return Ok(modulePermissions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al obtener permisos de módulo", error = ex.Message });
            }
        }

        [HttpGet("GetUserModuleLines/{employeeNumber}/{moduleId}")]
        public async Task<IActionResult> GetUserModuleLines(string employeeNumber, int moduleId)
        {
            try
            {
                var userLines = await _context.UserLinePermissions
                    .Where(ulp => ulp.EmployeeNumber == employeeNumber)
                    .Select(ulp => new
                    {
                        productionLinesId = ulp.ProductionLinesId
                    })
                    .Distinct()
                    .ToListAsync();

                return Ok(userLines);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al obtener líneas del usuario", error = ex.Message });
            }
        }

        [HttpGet("GetUserAreaLines/{employeeNumber}/{areaId}")]
        public async Task<IActionResult> GetUserAreaLines(string employeeNumber, int areaId)
        {
            try
            {
                var userLines = await _context.UserLinePermissions
                    .Include(ulp => ulp.ProductionLines)
                    .Where(ulp => ulp.EmployeeNumber == employeeNumber && ulp.ProductionLines.AreaId == areaId)
                    .Select(ulp => new
                    {
                        productionLinesId = ulp.ProductionLinesId
                    })
                    .Distinct()
                    .ToListAsync();

                return Ok(userLines);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al obtener líneas del usuario para el área", error = ex.Message });
            }
        }

        [HttpGet("GetLinesByArea/{areaId}")]
        public async Task<IActionResult> GetLinesByArea(int areaId)
        {
            try
            {
                var lines = await _context.ProductionLines
                    .Where(pl => pl.AreaId == areaId)
                    .Select(pl => new
                    {
                        productionLinesId = pl.ProductionLinesId,
                        lineNumber = pl.LineNumber,
                        areaId = pl.AreaId
                    })
                    .OrderBy(pl => pl.lineNumber)
                    .ToListAsync();

                return Ok(lines);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al obtener líneas del área", error = ex.Message });
            }
        }

        [HttpGet("GetModuleInfo/{moduleId}")]
        public async Task<IActionResult> GetModuleInfo(int moduleId)
        {
            try
            {
                var module = await _context.Modules
                    .Where(m => m.ModuleId == moduleId)
                    .Select(m => new
                    {
                        moduleId = m.ModuleId,
                        moduleName = m.ModuleName,
                        description = m.Description,
                        route = m.Route,
                        isActive = m.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (module == null)
                {
                    return NotFound(new { message = "Módulo no encontrado" });
                }

                return Ok(module);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al obtener información del módulo", error = ex.Message });
            }
        }

        [HttpGet("CheckUserModuleAccess/{employeeNumber}/{moduleId}")]
        public async Task<IActionResult> CheckUserModuleAccess(string employeeNumber, int moduleId)
        {
            try
            {
                // Obtener información del módulo
                var module = await _context.Modules.FindAsync(moduleId);
                if (module == null)
                {
                    return NotFound(new { message = "Módulo no encontrado" });
                }

                // Verificar si el usuario tiene permiso para este módulo
                var userPermission = await _context.UserModulePermissions
                    .Include(ump => ump.Permission)
                    .Where(ump => ump.EmployeeNumber == employeeNumber && ump.ModuleId == moduleId)
                    .FirstOrDefaultAsync();

                // Obtener todos los permisos disponibles
                var allPermissions = await _context.Permissions.ToListAsync();

                var result = new
                {
                    employeeNumber = employeeNumber,
                    moduleId = moduleId,
                    moduleName = module.ModuleName,
                    moduleIsActive = module.IsActive,
                    hasPermission = userPermission != null,
                    permissionName = userPermission?.Permission?.Name,
                    permissionId = userPermission?.PermissionId,
                    canViewInHome = userPermission != null &&
                                    module.IsActive &&
                                    userPermission.Permission.Name == "View",
                    reason = GetAccessReason(module, userPermission),
                    availablePermissions = allPermissions.Select(p => new { p.PermissionId, p.Name }).ToList()
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al verificar acceso", error = ex.Message });
            }
        }

        private string GetAccessReason(Module module, UserModulePermission? userPermission)
        {
            if (!module.IsActive)
                return "El módulo está inactivo (IsActive = false)";

            if (userPermission == null)
                return "El usuario no tiene ningún permiso asignado para este módulo";

            if (userPermission.Permission.Name != "View")
                return $"El usuario tiene permiso '{userPermission.Permission.Name}' pero se requiere 'View' para ver el módulo en Home";

            return "El usuario tiene acceso correcto al módulo";
        }

        [HttpPost("SaveUserPermissions")]
        public async Task<IActionResult> SaveUserPermissions([FromBody] SavePermissionsRequest request)
        {
            try
            {
                // Eliminar permisos existentes
                var existingAreaPermissions = await _context.UserAreaPermissions
                    .Where(uap => uap.EmployeeNumber == request.EmployeeNumber.ToString())
                    .ToListAsync();

                var existingLinePermissions = await _context.UserLinePermissions
                    .Where(ulp => ulp.EmployeeNumber == request.EmployeeNumber.ToString())
                    .ToListAsync();

                _context.UserAreaPermissions.RemoveRange(existingAreaPermissions);
                _context.UserLinePermissions.RemoveRange(existingLinePermissions);

                // Agregar nuevos permisos de área
                foreach (var areaPerm in request.AreaPermissions)
                {
                    _context.UserAreaPermissions.Add(new UserAreaPermission
                    {
                        EmployeeNumber = areaPerm.EmployeeNumber,
                        AreaId = areaPerm.AreaId,
                        PermissionId = areaPerm.PermissionId
                    });
                }

                // Agregar nuevos permisos de línea
                foreach (var linePerm in request.LinePermissions)
                {
                    _context.UserLinePermissions.Add(new UserLinePermission
                    {
                        EmployeeNumber = linePerm.EmployeeNumber,
                        ProductionLinesId = linePerm.ProductionLinesId,
                        PermissionId = linePerm.PermissionId
                    });
                }

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Permisos guardados correctamente" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error al guardar permisos", error = ex.Message });
            }
        }

        [HttpPost("SaveUserModulePermissions")]
        public async Task<IActionResult> SaveUserModulePermissions([FromBody] SaveModulePermissionsRequest request)
        {
            try
            {
                // Validar que el usuario existe
                var userExists = await _context.Users.AnyAsync(u => u.EmployeeNumber == request.EmployeeNumber.ToString());
                if (!userExists)
                {
                    return BadRequest(new { success = false, message = "El usuario no existe" });
                }

                // Validar que el módulo existe
                var moduleExists = await _context.Modules.AnyAsync(m => m.ModuleId == request.ModuleId);
                if (!moduleExists)
                {
                    return BadRequest(new { success = false, message = "El módulo no existe" });
                }

                // Validar que el área existe
                if (request.AreaId.HasValue)
                {
                    var areaExists = await _context.Areas.AnyAsync(a => a.AreaId == request.AreaId.Value);
                    if (!areaExists)
                    {
                        return BadRequest(new { success = false, message = "El área no existe" });
                    }
                }

                // Eliminar permisos existentes del usuario para este módulo
                var existingPermissions = await _context.UserModulePermissions
                    .Where(ump => ump.EmployeeNumber == request.EmployeeNumber.ToString() && ump.ModuleId == request.ModuleId)
                    .ToListAsync();

                _context.UserModulePermissions.RemoveRange(existingPermissions);

                // Agregar nuevos permisos del módulo
                foreach (var perm in request.Permissions)
                {
                    _context.UserModulePermissions.Add(new UserModulePermission
                    {
                        EmployeeNumber = perm.EmployeeNumber,
                        ModuleId = perm.ModuleId,
                        PermissionId = perm.PermissionId
                    });
                }

                // Si hay un área seleccionada, gestionar permisos de área y líneas
                if (request.AreaId.HasValue)
                {
                    // Eliminar permisos de área existentes del usuario para esta área
                    var existingAreaPermissions = await _context.UserAreaPermissions
                        .Where(uap => uap.EmployeeNumber == request.EmployeeNumber.ToString() && uap.AreaId == request.AreaId.Value)
                        .ToListAsync();

                    _context.UserAreaPermissions.RemoveRange(existingAreaPermissions);

                    // Eliminar líneas existentes del usuario para esta área
                    var existingLines = await _context.UserLinePermissions
                        .Include(ulp => ulp.ProductionLines)
                        .Where(ulp => ulp.EmployeeNumber == request.EmployeeNumber.ToString() && ulp.ProductionLines.AreaId == request.AreaId.Value)
                        .ToListAsync();

                    _context.UserLinePermissions.RemoveRange(existingLines);

                    // Siempre guardar permisos de área (para validaciones generales)
                    foreach (var perm in request.Permissions)
                    {
                        _context.UserAreaPermissions.Add(new UserAreaPermission
                        {
                            EmployeeNumber = request.EmployeeNumber.ToString(),
                            AreaId = request.AreaId.Value,
                            PermissionId = perm.PermissionId
                        });
                    }

                    // Si hay líneas seleccionadas, guardar permisos específicos de líneas
                    if (request.Lines != null && request.Lines.Any())
                    {
                        foreach (var line in request.Lines)
                        {
                            foreach (var perm in request.Permissions)
                            {
                                _context.UserLinePermissions.Add(new UserLinePermission
                                {
                                    EmployeeNumber = line.EmployeeNumber,
                                    ProductionLinesId = line.ProductionLinesId,
                                    PermissionId = perm.PermissionId
                                });
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Permisos guardados correctamente" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error al guardar permisos", error = ex.Message });
            }
        }
    }

    // DTOs para las requests
    public class AddUserRequest
    {
        public int EmployeeNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? Area { get; set; }
    }

    public class SavePermissionsRequest
    {
        public int EmployeeNumber { get; set; }
        public List<AreaPermissionDto> AreaPermissions { get; set; } = new();
        public List<LinePermissionDto> LinePermissions { get; set; } = new();
    }

    public class AreaPermissionDto
    {
        public string EmployeeNumber { get; set; }
        public int AreaId { get; set; }
        public int PermissionId { get; set; }
    }

    public class LinePermissionDto
    {
        public string EmployeeNumber { get; set; }
        public int ProductionLinesId { get; set; }
        public int PermissionId { get; set; }
    }

    public class SaveModulePermissionsRequest
    {
        public int EmployeeNumber { get; set; }
        public int ModuleId { get; set; }
        public int? AreaId { get; set; }
        public List<ModulePermissionDto> Permissions { get; set; } = new();
        public List<ModuleLineDto> Lines { get; set; } = new();
    }

    public class ModulePermissionDto
    {
        public string EmployeeNumber { get; set; }
        public int ModuleId { get; set; }
        public int PermissionId { get; set; }
    }

    public class ModuleLineDto
    {
        public string EmployeeNumber { get; set; }
        public int ModuleId { get; set; }
        public int? AreaId { get; set; }
        public int ProductionLinesId { get; set; }
    }
}