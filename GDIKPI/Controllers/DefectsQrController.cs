using GDIKPI.Data;
using GDIKPI.DTO.DefectsQr;
using GDIKPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GDIKPI.Controllers
{
    public class DefectsQrController : Controller
    {
        private readonly KpisContext _context;
        private readonly PermissionService _permissionService;

        public DefectsQrController(KpisContext context, PermissionService permissionService)
        {
            _context = context;
            _permissionService = permissionService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? areaId = null)
        {
            var employeeNumber = User.FindFirst("EmployeeNumber")?.Value;
            if (string.IsNullOrWhiteSpace(employeeNumber))
            {
                return RedirectToAction("Login", "Account");
            }

            var accessibleAreaIds = await _permissionService.GetAccessibleAreasIncludingAdmin(employeeNumber, "View");
            if (!accessibleAreaIds.Any())
            {
                return Forbid();
            }

            var areas = await _context.Areas
                .AsNoTracking()
                .Where(a => accessibleAreaIds.Contains(a.AreaId))
                .OrderBy(a => a.CustomerName)
                .ThenBy(a => a.AreaName)
                .Select(a => new AreaOptionDto
                {
                    AreaId = a.AreaId,
                    DisplayName = $"{a.CustomerName} - {a.AreaName}"
                })
                .ToListAsync();

            var selectedAreaId = areaId.HasValue && accessibleAreaIds.Contains(areaId.Value)
                ? areaId.Value
                : areas.First().AreaId;

            var selectedArea = await _context.Areas
                .AsNoTracking()
                .FirstAsync(a => a.AreaId == selectedAreaId);

            var defects = await _context.Defects
                .AsNoTracking()
                .Include(d => d.DefectCategory)
                .Where(d => d.AreaId == selectedAreaId)
                .OrderBy(d => d.DefectName)
                .Select(d => new DefectQrItemDto
                {
                    DefectId = d.DefectId,
                    DefectName = d.DefectName,
                    CategoryName = d.DefectCategory != null ? d.DefectCategory.DefectCategoryName : null,
                    QrValue = d.DefectId.ToString()
                })
                .ToListAsync();

            var allActiveCommands = await _context.Commands
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.CommandText)
                .ToListAsync();

            var commands = allActiveCommands
                .Select(c => new CommandQrItemDto
                {
                    CommandText = c.CommandText,
                    ActionKey = c.ActionKey,
                    Scope = c.Scope,
                    Description = string.IsNullOrWhiteSpace(c.Description) ? c.ActionKey : c.Description,
                    QrValue = c.CommandText
                })
                .ToList();

            var viewModel = new DefectsQrPageViewModel
            {
                SelectedAreaId = selectedAreaId,
                SelectedAreaName = $"{selectedArea.CustomerName} - {selectedArea.AreaName}",
                Areas = areas,
                Defects = defects,
                Commands = commands
            };

            return View(viewModel);
        }

    }
}
