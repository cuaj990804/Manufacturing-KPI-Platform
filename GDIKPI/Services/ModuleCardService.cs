using GDIKPI.Data;
using GDIKPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GDIKPI.Services
{
    public class ModuleCardService
    {
        private readonly KpisContext _context;
        private readonly PermissionService _permissionService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ModuleCardService(KpisContext context, PermissionService permissionService, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _permissionService = permissionService;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ModuleCardsViewModel> GetUserModuleCards(int employeeNumber)
        {
            // Primero intentar con el nuevo sistema de UserModulePermission
            var newSystemCards = await GetUserModuleCardsFromDatabase(employeeNumber);
            if (newSystemCards.HasAnyCards())
            {
                return newSystemCards;
            }

            // Fallback al sistema anterior
            return await GetUserModuleCardsLegacy(employeeNumber);
        }

        // Nuevo método que usa UserModulePermission
        public async Task<ModuleCardsViewModel> GetUserModuleCardsFromDatabase(int employeeNumber)
        {
            var moduleCards = new ModuleCardsViewModel();

            var employeeNumberStr = employeeNumber.ToString();

            // Obtener módulos permitidos para el usuario
            var userModules = await _context.UserModulePermissions
                .Include(ump => ump.Module)
                .Include(ump => ump.Permission)
                .Where(ump => ump.EmployeeNumber == employeeNumberStr &&
                             ump.Module.IsActive &&
                             ump.Permission.Name == "View") // Solo módulos con permiso de vista
                .Select(ump => ump.Module)
                .Distinct()
                .ToListAsync();

            // Verificar si es administrador (PermissionID = 5 según tus datos)
            var isAdmin = await _context.UserModulePermissions
                .AnyAsync(ump => ump.EmployeeNumber == employeeNumberStr && ump.PermissionId == 5);

            // Obtener áreas accesibles del usuario
            var accessibleAreaIds = await _permissionService.GetAccessibleAreasIncludingAdmin(employeeNumber.ToString(), "View");
            var accessibleAreas = await _context.Areas
                .Where(a => accessibleAreaIds.Contains(a.AreaId))
                .ToListAsync();

            // Convertir módulos a cards organizadas por categoría
            moduleCards.ProductionCards = new List<ModuleCard>();
            moduleCards.QualityCards = new List<ModuleCard>();
            moduleCards.OEECards = new List<ModuleCard>();
            moduleCards.AdminCards = new List<ModuleCard>();

            foreach (var module in userModules)
            {
                // Crear la card base del módulo
                var baseCard = await CreateModuleCard(module);

                // Determinar si el módulo requiere áreas
                bool requiresArea = RequiresAreaParameter(module);

                if (requiresArea && accessibleAreas.Any())
                {
                    // Crear una card por cada área accesible
                    foreach (var area in accessibleAreas)
                    {
                        var card = await CreateModuleCard(module);
                        card.RouteValues = new { areaId = area.AreaId };

                        // Personalizar título y descripción según el tipo de módulo
                        if (IsAnalyticsModule(module))
                        {
                            if (module.Route?.Contains("DashboardProduction") == true ||
                                module.ModuleName.Contains("Dashboard Producción") ||
                                module.ModuleName.Contains("Dashboard Production"))
                            {
                                card.Title = "Dashboard Producción";
                                card.Description = $"Métricas de producción del área {area.CustomerName} - {area.AreaName}";
                            }
                            else if (module.Route?.Contains("QualityDashboard") == true ||
                                     module.ModuleName.Contains("Dashboard Calidad") ||
                                     module.ModuleName.Contains("Dashboard Quality"))
                            {
                                card.Title = "Dashboard Calidad";
                                card.Description = $"Métricas de calidad del área {area.CustomerName} - {area.AreaName}";
                            }
                            else
                            {
                                card.Description = $"Métricas del área {area.CustomerName} - {area.AreaName}";
                            }
                            moduleCards.OEECards.Add(card);
                        }
                        else if (IsProductionModule(module))
                        {
                            card.Title = $"Área {area.CustomerName} {area.AreaName}";
                            card.Description = "Registro de producción";
                            moduleCards.ProductionCards.Add(card);
                        }
                        else if (IsQualityModule(module))
                        {
                            card.Title = "Registro de rechazos";
                            card.Description = $"{area.CustomerName} - {area.AreaName}";
                            moduleCards.QualityCards.Add(card);
                        }
                    }
                }
                else
                {
                    // Módulos que no requieren área o no tiene áreas asignadas
                    // Mostrar la card sin parámetro de área
                    if (IsAnalyticsModule(module))
                    {
                        moduleCards.OEECards.Add(baseCard);
                    }
                    else if (IsProductionModule(module))
                    {
                        moduleCards.ProductionCards.Add(baseCard);
                    }
                    else if (IsQualityModule(module))
                    {
                        moduleCards.QualityCards.Add(baseCard);
                    }
                    else if (IsAdminModule(module) && isAdmin)
                    {
                        moduleCards.AdminCards.Add(baseCard);
                    }
                    else
                    {
                        // Si no se clasifica en ninguna categoría, agregarlo a la más apropiada
                        moduleCards.OEECards.Add(baseCard);
                    }
                }
            }

            AddDefectsQrQualityCard(moduleCards, isAdmin || userModules.Any(IsQualityModule));
            return moduleCards;
        }

        // Sistema anterior como fallback
        public async Task<ModuleCardsViewModel> GetUserModuleCardsLegacy(int employeeNumber)
        {
            var moduleCards = new ModuleCardsViewModel();

            // Verificar si es administrador
            var isAdmin = await _permissionService.IsUserAdmin(employeeNumber.ToString());

            // Obtener áreas accesibles
            var accessibleAreaIds = await _permissionService.GetAccessibleAreasIncludingAdmin(employeeNumber.ToString(), "View");
            var accessibleAreas = await _context.Areas
                .Where(a => accessibleAreaIds.Contains(a.AreaId))
                .ToListAsync();

            // Obtener permisos del usuario
            var userPermissions = await _permissionService.GetUserPermissions(employeeNumber.ToString());

            // Obtener información del usuario
            var user = await _context.Users.FirstOrDefaultAsync(u => u.EmployeeNumber == employeeNumber.ToString());

            // Cargar módulos según permisos
            await LoadProductionModules(moduleCards, accessibleAreas, userPermissions, isAdmin, user, employeeNumber);
            await LoadQualityModules(moduleCards, accessibleAreas, userPermissions, isAdmin, user);
            await LoadOEEModules(moduleCards, accessibleAreas, userPermissions, isAdmin, user);
            await LoadAdminModules(moduleCards, userPermissions, isAdmin);

            return moduleCards;
        }

        private async Task LoadProductionModules(ModuleCardsViewModel moduleCards, List<Area> accessibleAreas, List<string> userPermissions, bool isAdmin, User? user, int employeeNumber = 0)
        {
            // Verificar si tiene permisos de producción
            bool hasProductionPermission = isAdmin ||
                userPermissions.Any(p => p.Equals("View", StringComparison.OrdinalIgnoreCase) ||
                                        p.Equals("Create", StringComparison.OrdinalIgnoreCase) ||
                                        p.Equals("Edit", StringComparison.OrdinalIgnoreCase)) ||
                HasRequiredArea(user?.Area, new[] { "production", "manufacturing" });

            if (hasProductionPermission)
            {
                moduleCards.ProductionCards = new List<ModuleCard>();

                if (accessibleAreas.Any())
                {
                    // Cards por área específica (permisos de área)
                    foreach (var area in accessibleAreas)
                    {
                        moduleCards.ProductionCards.Add(new ModuleCard
                        {
                            Title = $"Área {area.CustomerName} {area.AreaName}",
                            Description = "Registro de producción",
                            Controller = "ProductionData",
                            Action = "Index",
                            RouteValues = new { areaId = area.AreaId },
                            Icon = "fas fa-industry",
                            Category = "Producciódn"
                        });
                    }
                }
                else
                {
                    // Si no tiene permisos de área directos, buscar áreas de las líneas accesibles
                    var accessibleLines = await _permissionService.GetAllAccessibleLines(employeeNumber.ToString(), "create");
                    if (accessibleLines.Any())
                    {
                        var areasFromLines = await _context.ProductionLines
                            .Where(pl => accessibleLines.Contains(pl.ProductionLinesId) && pl.IsActive)
                            .Include(pl => pl.Area)
                            .Select(pl => pl.Area)
                            .Distinct()
                            .ToListAsync();

                        foreach (var area in areasFromLines)
                        {
                            if (area != null)
                            {
                                moduleCards.ProductionCards.Add(new ModuleCard
                                {
                                    Title = $"Área {area.CustomerName} {area.AreaName}",
                                    Description = "Registro de producción",
                                    Controller = "ProductionData",
                                    Action = "Index",
                                    RouteValues = new { areaId = area.AreaId },
                                    Icon = "fas fa-industry",
                                    Category = "Producción"
                                });
                            }
                        }
                    }
                }

                

                // Card de registro de áreas (solo para admins o usuarios con permisos especiales)
                if (isAdmin || userPermissions.Contains("admin") || userPermissions.Contains("area_management"))
                {
                    moduleCards.ProductionCards.Add(new ModuleCard
                    {
                        Title = "Registro de Áreas",
                        Description = "Gestión de áreas",
                        Controller = "Area",
                        Action = "Index",
                        Icon = "fas fa-map-marked-alt",
                        Category = "Producción"
                    });
                }
            }
        }

        private async Task LoadQualityModules(ModuleCardsViewModel moduleCards, List<Area> accessibleAreas, List<string> userPermissions, bool isAdmin, User? user)
        {
            // Verificar si tiene permisos de calidad
            bool hasQualityPermission = isAdmin ||
                userPermissions.Any(p => p.Contains("quality") || p.Contains("defect") || p.Contains("reject")) ||
                HasRequiredArea(user?.Area, new[] { "quality", "calidad" });

            // Verificar si es supervisor/manager de quality
            bool isQualitySupervisorOrManager = user != null &&
                HasRequiredRole(user.Role, new[] { "supervisor", "manager", "admin" }) &&
                HasRequiredArea(user.Area, new[] { "quality", "calidad" });

            if (hasQualityPermission)
            {
                if (accessibleAreas.Any())
                {
                    moduleCards.QualityCards = new List<ModuleCard>();

                    // Cards de registro de rechazos por área
                    foreach (var area in accessibleAreas)
                    {
                        moduleCards.QualityCards.Add(new ModuleCard
                        {
                            Title = "Registro de rechazos",
                            Description = $"{area.CustomerName} - {area.AreaName}",
                            Controller = "Defects",
                            Action = "RegisterDefects",
                            RouteValues = new { areaId = area.AreaId },
                            Icon = "fas fa-exclamation-triangle",
                            Category = "Rechazos"
                        });
                    }
                }
                else if (isQualitySupervisorOrManager)
                {
                    // Si es supervisor/manager de quality sin áreas asignadas, obtener todas las áreas
                    moduleCards.QualityCards = new List<ModuleCard>();

                    var allAreas = await _context.Areas.ToListAsync();
                    foreach (var area in allAreas)
                    {
                        moduleCards.QualityCards.Add(new ModuleCard
                        {
                            Title = "Registro de rechazos",
                            Description = $"{area.CustomerName} - {area.AreaName}",
                            Controller = "Defects",
                            Action = "RegisterDefects",
                            RouteValues = new { areaId = area.AreaId },
                            Icon = "fas fa-exclamation-triangle",
                            Category = "Rechazos"
                        });
                    }
                }
            }

            AddDefectsQrQualityCard(moduleCards, hasQualityPermission);
        }

        private async Task LoadOEEModules(ModuleCardsViewModel moduleCards, List<Area> accessibleAreas, List<string> userPermissions, bool isAdmin, User? user)
        {
            if (user == null) return;

            moduleCards.OEECards = new List<ModuleCard>();

            // Verificar permisos directamente desde UserModulePermissions
            var userModulePermissions = await _context.UserModulePermissions
                .Include(ump => ump.Module)
                .Include(ump => ump.Permission)
                .Where(ump => ump.EmployeeNumber == user.EmployeeNumber.ToString() &&
                             ump.Module.IsActive &&
                             ump.Permission.Name == "View")
                .ToListAsync();

            // Dashboard OEE - Verificar si tiene permiso para el módulo
            var oeeModule = userModulePermissions
                .FirstOrDefault(ump => ump.Module.ModuleName.Contains("OEE") ||
                                      ump.Module.Route?.Contains("OEE") == true);

            if (oeeModule != null || isAdmin)
            {
                moduleCards.OEECards.Add(new ModuleCard
                {
                    Title = "Dashboard OEE",
                    Description = "Eficiencia de equipos",
                    Controller = "OEE",
                    Action = "Index",
                    Icon = "fas fa-chart-line",
                    Category = "Dashboards"
                });
            }

            // Dashboard Calidad - Verificar si tiene permiso para el módulo
            var qualityDashboardModule = userModulePermissions
                .FirstOrDefault(ump => ump.Module.ModuleName.Contains("Dashboard Quality") ||
                                      ump.Module.ModuleName.Contains("Dashboard Calidad") ||
                                      ump.Module.Route?.Contains("QualityDashboard") == true);

            if (qualityDashboardModule != null || isAdmin)
            {
                moduleCards.OEECards.Add(new ModuleCard
                {
                    Title = "Dashboard Calidad",
                    Description = "Métricas de calidad",
                    Controller = "QualityDashboard",
                    Action = "Index",
                    Icon = "fas fa-chart-bar",
                    Category = "Dashboards"
                });
            }

            // Dashboard Producción - Verificar si tiene permiso para el módulo
            var productionDashboardModule = userModulePermissions
                .FirstOrDefault(ump => ump.Module.ModuleName.Contains("Dashboard Producción") ||
                                      ump.Module.ModuleName.Contains("Dashboard Production") ||
                                      ump.Module.Route?.Contains("DashboardProduction") == true);

            if (productionDashboardModule != null || isAdmin)
            {
                if (accessibleAreas.Any())
                {
                    // Crear una card de Dashboard de Producción por cada área accesible
                    foreach (var area in accessibleAreas)
                    {
                        moduleCards.OEECards.Add(new ModuleCard
                        {
                            Title = $"Dashboard Producción",
                            Description = $"Métricas de producción del área {area.CustomerName} {area.AreaName}",
                            Controller = "DashboardProduction",
                            Action = "Index",
                            RouteValues = new { areaId = area.AreaId },
                            Icon = "fas fa-chart-pie",
                            Category = "Dashboards"
                        });
                    }
                }
                else
                {
                    // Si no tiene áreas pero tiene el permiso del módulo, mostrar sin área específica
                    moduleCards.OEECards.Add(new ModuleCard
                    {
                        Title = "Dashboard Producción",
                        Description = "Métricas de producción",
                        Controller = "DashboardProduction",
                        Action = "Index",
                        Icon = "fas fa-chart-pie",
                        Category = "Dashboards"
                    });
                }
            }

            // Dashboard Eficiencia - Verificar si tiene permiso para el módulo
            var efficiencyDashboardModule = userModulePermissions
                .FirstOrDefault(ump => ump.Module.ModuleName.Contains("Dashboard Eficiencia") ||
                                      ump.Module.ModuleName.Contains("Dashboard Efficiency") ||
                                      ump.Module.Route?.Contains("Efficiency") == true);

            if (efficiencyDashboardModule != null || isAdmin)
            {
                moduleCards.OEECards.Add(new ModuleCard
                {
                    Title = "Dashboard Eficiencia",
                    Description = "Métricas de eficiencia",
                    Controller = "Efficiency",
                    Action = "Index",
                    Icon = "fas fa-tachometer-alt",
                    Category = "Dashboards"
                });
            }

            var productionOperatorsDashboardModule = userModulePermissions
                .FirstOrDefault(ump => ump.Module.ModuleName.Contains("Dashboard Escaneos") ||
                                      ump.Module.ModuleName.Contains("Production Operators Dashboard") ||
                                      ump.Module.Route?.Contains("ProductionOperatorsDashboard") == true);

            if (productionOperatorsDashboardModule != null || isAdmin)
            {
                moduleCards.OEECards.Add(new ModuleCard
                {
                    Title = "Dashboard Escaneos",
                    Description = "Visualizacion de escaneos de operadores",
                    Controller = "ProductionOperatorsDashboard",
                    Action = "Index",
                    Icon = "fas fa-barcode",
                    Category = "Dashboards"
                });
            }

            var attendanceDashboardModule = userModulePermissions
                .FirstOrDefault(ump => ump.Module.ModuleName.Contains("Dashboard Asistencia") ||
                                      ump.Module.ModuleName.Contains("Attendance Dashboard") ||
                                      ump.Module.Route?.Contains("AttendanceDashboard") == true);

            if (attendanceDashboardModule != null || isAdmin)
            {
                moduleCards.OEECards.Add(new ModuleCard
                {
                    Title = "Dashboard Asistencia",
                    Description = "Consulta de asistencia y ausencias por departamento",
                    Controller = "AttendanceDashboard",
                    Action = "Index",
                    Icon = "fas fa-user-check",
                    Category = "Dashboards"
                });
            }
        }

        private async Task LoadAdminModules(ModuleCardsViewModel moduleCards, List<string> userPermissions, bool isAdmin)
        {
            if (isAdmin || userPermissions.Contains("admin") || userPermissions.Contains("user_management"))
            {
                moduleCards.AdminCards = new List<ModuleCard>();

                moduleCards.AdminCards.Add(new ModuleCard
                {
                    Title = "Panel de Control",
                    Description = "Gestión de usuarios y permisos",
                    Controller = "PanelControl",
                    Action = "Index",
                    Icon = "fas fa-users-cog",
                    Category = "Administración"
                });
            }
        }

        // Métodos helper
        private int GetCurrentEmployeeNumber()
        {
            var employeeNumberClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("EmployeeNumber")?.Value;
            return int.TryParse(employeeNumberClaim, out int employeeNumber) ? employeeNumber : 0;
        }

        private bool HasRequiredRole(string? userRole, string[] requiredRoles)
        {
            if (string.IsNullOrEmpty(userRole)) return false;
            return requiredRoles.Any(role => userRole.ToLower().Contains(role.ToLower()));
        }

        private bool HasRequiredArea(string? userArea, string[] requiredAreas)
        {
            if (string.IsNullOrEmpty(userArea)) return false;
            return requiredAreas.Any(area => userArea.ToLower().Contains(area.ToLower()));
        }

        // Métodos helper para el nuevo sistema
        private async Task<ModuleCard> CreateModuleCard(Module module)
        {
            return new ModuleCard
            {
                Title = module.ModuleName,
                Description = module.Description ?? "",
                Controller = ExtractControllerFromRoute(module.Route),
                Action = ExtractActionFromRoute(module.Route),
                RouteValues = ExtractRouteValuesFromRoute(module.Route),
                Icon = GetIconForModule(module.ModuleName),
                Category = GetCategoryForModule(module.ModuleName)
            };
        }

        private bool IsProductionModule(Module module)
        {
            var productionKeywords = new[] { "producción", "production", "línea", "line", "manufactur" };
            return productionKeywords.Any(keyword =>
                module.ModuleName.ToLower().Contains(keyword) ||
                (module.Route?.ToLower().Contains(keyword) ?? false));
        }

        private bool IsQualityModule(Module module)
        {
            var qualityKeywords = new[] { "calidad", "quality", "defecto", "defect", "rechazo", "reject" };
            return qualityKeywords.Any(keyword =>
                module.ModuleName.ToLower().Contains(keyword) ||
                (module.Route?.ToLower().Contains(keyword) ?? false));
        }

        private bool IsAnalyticsModule(Module module)
        {
            var analyticsKeywords = new[] { "dashboard", "oee", "métrica", "metric", "analytic", "reporte", "report" };
            return analyticsKeywords.Any(keyword =>
                module.ModuleName.ToLower().Contains(keyword) ||
                (module.Route?.ToLower().Contains(keyword) ?? false));
        }

        private bool IsAdminModule(Module module)
        {
            var adminKeywords = new[] { "admin", "usuario", "user", "gestión", "management", "configuración", "setting" };
            return adminKeywords.Any(keyword =>
                module.ModuleName.ToLower().Contains(keyword) ||
                (module.Route?.ToLower().Contains(keyword) ?? false));
        }

        private bool IsAreaManagementModule(Module module)
        {
            // Módulos de gestión de áreas que no requieren áreas asignadas para mostrarse
            var areaManagementKeywords = new[] { "registro de areas", "gestión de áreas", "area management", "/area/" };
            return areaManagementKeywords.Any(keyword =>
                module.ModuleName.ToLower().Contains(keyword) ||
                (module.Route?.ToLower().Contains(keyword) ?? false));
        }

        private bool RequiresAreaParameter(Module module)
        {
            // Determinar si el módulo requiere el parámetro areaId en su ruta
            // La mayoría de los módulos de producción, calidad y dashboards requieren área
            // excepto algunos módulos administrativos o de configuración

            // Módulos que NO requieren área
            if (IsAreaManagementModule(module)) return false;
            if (module.ModuleName.Contains("Panel de Control")) return false;
            if (module.ModuleName.Contains("Gestión de Usuarios")) return false;
            if (module.Route?.Contains("PanelControl") == true) return false;
            if (module.Route?.Contains("/OEE/") == true && !module.Route.Contains("DashboardProduction")) return false;

            // Módulos que SÍ requieren área
            if (IsProductionModule(module)) return true;
            if (IsQualityModule(module)) return true;
            if (module.Route?.Contains("DashboardProduction") == true) return true;
            if (module.Route?.Contains("QualityDashboard") == true) return true;

            // Por defecto, no requiere área
            return false;
        }

        private string ExtractControllerFromRoute(string? route)
        {
            if (string.IsNullOrEmpty(route)) return "Home";

            var parts = route.TrimStart('/').Split('/');
            return parts.Length > 0 ? parts[0] : "Home";
        }

        private string ExtractActionFromRoute(string? route)
        {
            if (string.IsNullOrEmpty(route)) return "Index";

            var parts = route.TrimStart('/').Split('/');
            return parts.Length > 1 ? parts[1] : "Index";
        }

        private object? ExtractRouteValuesFromRoute(string? route)
        {
            // Si la ruta contiene parámetros como /Controller/Action/{id}
            // Por ahora retornamos null, pero se puede extender
            return null;
        }

        private string GetIconForModule(string moduleName)
        {
            var name = moduleName.ToLower();

            if (name.Contains("defecto") || name.Contains("defect") || name.Contains("calidad"))
                return "fas fa-exclamation-triangle";
            if (name.Contains("dashboard") || name.Contains("métrica"))
                return "fas fa-chart-line";
            if (name.Contains("producción") || name.Contains("production"))
                return "fas fa-industry";
            if (name.Contains("usuario") || name.Contains("user"))
                return "fas fa-users";
            if (name.Contains("admin") || name.Contains("gestión"))
                return "fas fa-cogs";
            if (name.Contains("reporte") || name.Contains("report"))
                return "fas fa-chart-bar";

            return "fas fa-square";
        }

        private string GetCategoryForModule(string moduleName)
        {
            var name = moduleName.ToLower();

            if (name.Contains("defecto") || name.Contains("calidad") || name.Contains("rechazo"))
                return "Calidad";
            if (name.Contains("dashboard") || name.Contains("métrica") || name.Contains("oee"))
                return "Dashboards";
            if (name.Contains("producción") || name.Contains("línea"))
                return "Producción";
            if (name.Contains("usuario") || name.Contains("admin") || name.Contains("gestión"))
                return "Administración";
            if (name.Contains("reporte"))
                return "Reportes";

            return "General";
        }

        private void AddDefectsQrQualityCard(ModuleCardsViewModel moduleCards, bool canAccess)
        {
            if (!canAccess)
            {
                return;
            }

            moduleCards.QualityCards ??= new List<ModuleCard>();

            bool exists = moduleCards.QualityCards.Any(c =>
                c.Controller.Equals("DefectsQr", StringComparison.OrdinalIgnoreCase) &&
                c.Action.Equals("Index", StringComparison.OrdinalIgnoreCase));

            if (!exists)
            {
                moduleCards.QualityCards.Add(new ModuleCard
                {
                    Title = "Impresión QR Defectos",
                    Description = "Imprimir QR de defectos y comandos por área",
                    Controller = "DefectsQr",
                    Action = "Index",
                    Icon = "fas fa-qrcode",
                    Category = "Rechazos"
                });
            }
        }
    }

    // ViewModels para organizar las cards
    public class ModuleCardsViewModel
    {
        public List<ModuleCard>? ProductionCards { get; set; }
        public List<ModuleCard>? QualityCards { get; set; }
        public List<ModuleCard>? OEECards { get; set; }
        public List<ModuleCard>? AdminCards { get; set; }

        public bool HasAnyCards()
        {
            return (ProductionCards?.Any() ?? false) ||
                   (QualityCards?.Any() ?? false) ||
                   (OEECards?.Any() ?? false) ||
                   (AdminCards?.Any() ?? false);
        }
    }

    public class ModuleCard
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Controller { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public object? RouteValues { get; set; }
        public string Icon { get; set; } = "fas fa-square";
        public string Category { get; set; } = string.Empty;
        public string CssClass { get; set; } = "card mb-3 shadow-sm";
        public bool IsVisible { get; set; } = true;
    }
}
