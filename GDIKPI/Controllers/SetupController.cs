using GDIKPI.Data;
using GDIKPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GDIKPI.Controllers
{
    public class SetupController : Controller
    {
        private readonly KpisContext _context;

        public SetupController(KpisContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> FindZFForradoArea()
        {
            try
            {
                // Buscar el área ZF-Forrado
                var area = await _context.Areas
                    .Where(a => a.CustomerName.ToLower().Contains("zf") &&
                               a.AreaName.ToLower().Contains("forrado"))
                    .FirstOrDefaultAsync();

                if (area != null)
                {
                    return Json(new
                    {
                        success = true,
                        areaId = area.AreaId,
                        customerName = area.CustomerName,
                        areaName = area.AreaName,
                        fullName = $"{area.CustomerName} {area.AreaName}"
                    });
                }
                else
                {
                    // Buscar todas las áreas para ver qué tenemos
                    var allAreas = await _context.Areas
                        .Select(a => new { a.AreaId, a.CustomerName, a.AreaName })
                        .ToListAsync();

                    return Json(new
                    {
                        success = false,
                        message = "Área ZF-Forrado no encontrada",
                        availableAreas = allAreas
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Error: {ex.Message}"
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateTVUser(int areaId)
        {
            try
            {
                // Verificar si el usuario TV ya existe
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.EmployeeNumber == "999999");

                if (existingUser != null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Usuario TV ya existe",
                        user = existingUser
                    });
                }

                // Verificar que el área existe
                var area = await _context.Areas.FindAsync(areaId);
                if (area == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Área no encontrada"
                    });
                }

                // Crear el usuario TV
                var tvUser = new User
                {
                    EmployeeNumber = "999999",
                    Name = "TV",
                    Role = "tv",
                    Area = $"{area.CustomerName} {area.AreaName}"
                };

                _context.Users.Add(tvUser);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Usuario TV creado exitosamente",
                    user = tvUser,
                    areaId = areaId
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Error al crear usuario: {ex.Message}"
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateUserArea(string employeeNumber, int areaId)
        {
            try
            {
                // Buscar el usuario
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.EmployeeNumber == employeeNumber);

                if (user == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Usuario no encontrado"
                    });
                }

                // Buscar el área
                var area = await _context.Areas.FindAsync(areaId);
                if (area == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Área no encontrada"
                    });
                }

                // Actualizar el área del usuario
                user.Area = $"{area.CustomerName} {area.AreaName}";
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Usuario actualizado exitosamente",
                    user = new
                    {
                        employeeNumber = user.EmployeeNumber,
                        name = user.Name,
                        role = user.Role,
                        area = user.Area
                    },
                    areaId = areaId
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Error al actualizar usuario: {ex.Message}"
                });
            }
        }
    }
}