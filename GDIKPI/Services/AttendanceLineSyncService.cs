using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using GDIKPI.Data;
using GDIKPI.Models;
using Microsoft.EntityFrameworkCore;

namespace GDIKPI.Services
{
    public class AttendanceLineSyncService
    {
        private readonly KpisContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly AuditService _auditService;
        private readonly ILogger<AttendanceLineSyncService> _logger;
        private readonly string _attendanceApiBaseUrl;
        private readonly int _timeoutSeconds;

        public AttendanceLineSyncService(
            KpisContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            AuditService auditService,
            ILogger<AttendanceLineSyncService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _auditService = auditService;
            _logger = logger;
            _attendanceApiBaseUrl = (_configuration["AttendanceApi:BaseUrl"] ?? "https://localhost:7132").TrimEnd('/');
            _timeoutSeconds = int.TryParse(_configuration["AttendanceApi:Timeout"], out var timeout) ? timeout : 30;
        }

        public async Task<AttendanceLineSyncResponse> SyncAsync(DateOnly? targetDate = null, int? productionLineId = null, CancellationToken cancellationToken = default)
        {
            var date = targetDate ?? DateOnly.FromDateTime(DateTime.Today);
            var attendanceRows = await GetArrayAsync("api/Attendance/history", new Dictionary<string, string?>
            {
                ["start"] = date.ToString("yyyy-MM-dd"),
                ["end"] = date.ToString("yyyy-MM-dd")
            }, cancellationToken);

            var absenceRows = await GetArrayAsync("api/Attendance/absences", new Dictionary<string, string?>
            {
                ["start"] = date.ToString("yyyy-MM-dd"),
                ["end"] = date.ToString("yyyy-MM-dd")
            }, cancellationToken);

            var attendanceByDepartment = attendanceRows
                .Select(row => new
                {
                    Department = FirstString(row, "departamento", "department", "area"),
                    EmployeeKey = FirstString(row, "employeeNumber", "numeroEmpleado", "employeeCode", "badge")
                        ?? FirstString(row, "employeeId", "empleadoId", "idEmpleado", "id")
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Department) && !string.IsNullOrWhiteSpace(x.EmployeeKey))
                .GroupBy(x => x.Department!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.EmployeeKey!).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    StringComparer.OrdinalIgnoreCase);

            var absencesByDepartment = absenceRows
                .Select(row => new
                {
                    Department = FirstString(row, "departamento", "department", "area"),
                    EmployeeKey = FirstString(row, "employeeNumber", "numeroEmpleado", "employeeCode", "badge")
                        ?? FirstString(row, "employeeId", "empleadoId", "idEmpleado", "id")
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Department) && !string.IsNullOrWhiteSpace(x.EmployeeKey))
                .GroupBy(x => x.Department!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.EmployeeKey!).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    StringComparer.OrdinalIgnoreCase);

            var departments = attendanceByDepartment.Keys
                .Union(absencesByDepartment.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            if (departments.Count == 0)
            {
                return new AttendanceLineSyncResponse
                {
                    TargetDate = date,
                    Message = "No se encontraron departamentos con datos de asistencia o ausencias para esa fecha."
                };
            }

            var lines = await _context.ProductionLines
                .Include(line => line.Area)
                .Where(line => line.IsActive && (!productionLineId.HasValue || line.ProductionLinesId == productionLineId.Value))
                .ToListAsync(cancellationToken);

            if (productionLineId.HasValue && lines.Count == 0)
            {
                return new AttendanceLineSyncResponse
                {
                    TargetDate = date,
                    Message = $"La linea {productionLineId.Value} no existe o no esta activa."
                };
            }

            var results = new List<AttendanceLineSyncItemResult>();
            var matchedDepartments = new List<MatchedDepartmentLine>();

            foreach (var department in departments)
            {
                var resolvedLine = ResolveLineForDepartment(department, lines);
                if (resolvedLine is null)
                {
                    results.Add(new AttendanceLineSyncItemResult
                    {
                        Department = department,
                        Success = false,
                        Error = productionLineId.HasValue
                            ? "No fue posible asociar el departamento a la linea solicitada."
                            : "No fue posible asociar el departamento automaticamente con ProductionLines/Area."
                    });
                    continue;
                }

                matchedDepartments.Add(new MatchedDepartmentLine
                {
                    Department = department,
                    Line = resolvedLine,
                    AttendanceCount = attendanceByDepartment.TryGetValue(department, out var attendanceValue) ? attendanceValue : 0,
                    AbsencesCount = absencesByDepartment.TryGetValue(department, out var absenceValue) ? absenceValue : 0
                });
            }

            foreach (var lineGroup in matchedDepartments.GroupBy(item => item.Line.ProductionLinesId))
            {
                var line = lineGroup.First().Line;

                try
                {
                    var attendanceCount = lineGroup.Sum(item => item.AttendanceCount);
                    var absencesCount = lineGroup.Sum(item => item.AbsencesCount);
                    var oldValue = line.PersonalQuantity;

                    line.PersonalQuantity = attendanceCount;

                    if (oldValue != attendanceCount)
                    {
                        var lineLabel = !string.IsNullOrWhiteSpace(line.LineName)
                            ? line.LineName
                            : $"Linea {line.LineNumber}";
                        var departmentsSummary = string.Join(", ", lineGroup.Select(item => item.Department));

                        await _auditService.LogProductionLineAction(
                            "UPDATE",
                            line.ProductionLinesId,
                            $"Actualizacion de personal desde asistencia para {lineLabel} ({departmentsSummary}) el {date:yyyy-MM-dd}: {oldValue} → {attendanceCount}");
                    }

                    results.Add(new AttendanceLineSyncItemResult
                    {
                        Department = string.Join(", ", lineGroup.Select(item => item.Department)),
                        ProductionLineId = line.ProductionLinesId,
                        LineNumber = line.LineNumber,
                        LineName = line.LineName,
                        PreviousPeopleQuantity = oldValue,
                        NewPeopleQuantity = attendanceCount,
                        AttendanceCount = attendanceCount,
                        AbsencesCount = absencesCount,
                        Success = true
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sincronizando asistencia consolidada hacia linea {LineId}", line.ProductionLinesId);
                    results.Add(new AttendanceLineSyncItemResult
                    {
                        Department = string.Join(", ", lineGroup.Select(item => item.Department)),
                        ProductionLineId = line.ProductionLinesId,
                        Success = false,
                        Error = ex.Message
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            return new AttendanceLineSyncResponse
            {
                TargetDate = date,
                Items = results,
                Message = $"Sincronizacion completada. {results.Count(r => r.Success)} de {results.Count} lineas actualizadas."
            };
        }

        public List<object> GetMappings()
        {
            return _context.ProductionLines
                .Include(line => line.Area)
                .Where(line => line.IsActive)
                .OrderBy(line => line.LineNumber)
                .Select(line => new
                {
                    productionLineId = line.ProductionLinesId,
                    lineNumber = line.LineNumber,
                    lineName = line.LineName,
                    areaName = line.Area != null ? line.Area.AreaName : null,
                    customerName = line.Area != null ? line.Area.CustomerName : null
                })
                .ToList()
                .Cast<object>()
                .ToList();
        }

        private async Task<List<JsonNode?>> GetArrayAsync(string path, IDictionary<string, string?> query, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient("AttendanceApi");
            client.Timeout = TimeSpan.FromSeconds(_timeoutSeconds);

            var url = BuildUrl(path, query);
            using var response = await client.GetAsync(url, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(TryExtractErrorMessage(content) ?? $"Error al consultar asistencia ({(int)response.StatusCode}).");
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return new List<JsonNode?>();
            }

            var node = JsonNode.Parse(content);
            return ExtractArray(node);
        }

        private string BuildUrl(string path, IDictionary<string, string?> query)
        {
            var builder = new StringBuilder($"{_attendanceApiBaseUrl}/{path.TrimStart('/')}");
            var validQuery = query
                .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
                .Select(kvp => $"{WebUtility.UrlEncode(kvp.Key)}={WebUtility.UrlEncode(kvp.Value)}")
                .ToList();

            if (validQuery.Count > 0)
            {
                builder.Append('?');
                builder.Append(string.Join("&", validQuery));
            }

            return builder.ToString();
        }

        private ProductionLine? ResolveLineForDepartment(string department, List<ProductionLine> lines)
        {
            var normalizedDepartment = NormalizeKey(department);
            var parsed = ParseDepartment(normalizedDepartment);

            if (!string.IsNullOrWhiteSpace(parsed.AreaName) && !string.IsNullOrWhiteSpace(parsed.LineName))
            {
                var directAreaLineMatch = lines
                    .Where(line =>
                        line.AreaId.HasValue &&
                        NormalizeKey(line.Area?.AreaName) == parsed.AreaName &&
                        NormalizeKey(line.LineName) == parsed.LineName)
                    .ToList();

                if (directAreaLineMatch.Count == 1)
                {
                    return directAreaLineMatch[0];
                }

                var areaContainsProgramMatch = lines
                    .Where(line =>
                        line.AreaId.HasValue &&
                        NormalizeKey(line.Area?.AreaName) == parsed.AreaName &&
                        NormalizeKey($"{line.Area?.AreaName} {line.LineName}") == normalizedDepartment)
                    .ToList();

                if (areaContainsProgramMatch.Count == 1)
                {
                    return areaContainsProgramMatch[0];
                }

                var customerAndAreaLineMatch = lines
                    .Where(line =>
                        line.AreaId.HasValue &&
                        NormalizeKey($"{line.Area?.AreaName} {line.Area?.CustomerName}") == parsed.AreaName &&
                        NormalizeKey(line.LineName) == parsed.LineName)
                    .ToList();

                if (customerAndAreaLineMatch.Count == 1)
                {
                    return customerAndAreaLineMatch[0];
                }
            }

            var exactAreaAndLine = lines
                .Where(line =>
                    line.AreaId.HasValue &&
                    NormalizeKey($"{line.Area?.AreaName} {line.LineName}") == normalizedDepartment)
                .ToList();
            if (exactAreaAndLine.Count == 1)
            {
                return exactAreaAndLine[0];
            }

            var exactAreaCustomerAndLine = lines
                .Where(line => NormalizeKey($"{line.Area?.AreaName} {line.LineName}") == normalizedDepartment
                    || NormalizeKey($"{line.Area?.AreaName} {line.Area?.CustomerName} {line.LineName}") == normalizedDepartment
                    || NormalizeKey($"{line.Area?.CustomerName} {line.Area?.AreaName} {line.LineName}") == normalizedDepartment)
                .ToList();
            if (exactAreaCustomerAndLine.Count == 1)
            {
                return exactAreaCustomerAndLine[0];
            }

            return null;
        }

        private static ParsedDepartment ParseDepartment(string normalizedDepartment)
        {
            var tokens = normalizedDepartment.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2)
            {
                return new ParsedDepartment
                {
                    AreaName = normalizedDepartment,
                    LineName = string.Empty
                };
            }

            var areaName = tokens[0];
            var remainingTokens = tokens
                .Skip(1)
                .Where(token => !token.All(char.IsDigit))
                .ToList();

            if (remainingTokens.Count == 0)
            {
                return new ParsedDepartment
                {
                    AreaName = areaName,
                    LineName = string.Empty
                };
            }

            return new ParsedDepartment
            {
                AreaName = areaName,
                LineName = remainingTokens[^1]
            };
        }

        private static string NormalizeKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim().ToUpperInvariant();
            var chars = normalized
                .Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                .ToArray();

            return string.Join(" ", new string(chars)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private static List<JsonNode?> ExtractArray(JsonNode? node)
        {
            if (node is JsonArray array)
            {
                return array.ToList();
            }

            if (node is JsonObject obj)
            {
                foreach (var key in new[] { "data", "items", "results", "value" })
                {
                    if (obj[key] is JsonArray inner)
                    {
                        return inner.ToList();
                    }
                }
            }

            return new List<JsonNode?>();
        }

        private static string? FirstString(JsonNode? node, params string[] propertyNames)
        {
            if (node is not JsonObject obj)
            {
                return null;
            }

            foreach (var propertyName in propertyNames)
            {
                foreach (var property in obj)
                {
                    if (!property.Key.Equals(propertyName, StringComparison.OrdinalIgnoreCase) || property.Value == null)
                    {
                        continue;
                    }

                    var raw = property.Value.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(raw) && !raw.Equals("null", StringComparison.OrdinalIgnoreCase))
                    {
                        return raw;
                    }
                }
            }

            return null;
        }

        private static string? TryExtractErrorMessage(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            try
            {
                var node = JsonNode.Parse(content);
                return FirstString(node, "message", "error", "title", "detail");
            }
            catch
            {
                return content.Length <= 180 ? content : content[..180];
            }
        }
    }

    public class AttendanceLineSyncResponse
    {
        public DateOnly TargetDate { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<AttendanceLineSyncItemResult> Items { get; set; } = new();
    }

    public class AttendanceLineSyncItemResult
    {
        public string Department { get; set; } = string.Empty;
        public int ProductionLineId { get; set; }
        public int? LineNumber { get; set; }
        public string? LineName { get; set; }
        public int PreviousPeopleQuantity { get; set; }
        public int NewPeopleQuantity { get; set; }
        public int AttendanceCount { get; set; }
        public int AbsencesCount { get; set; }
        public bool Success { get; set; }
        public string? Error { get; set; }
    }

    internal class MatchedDepartmentLine
    {
        public string Department { get; set; } = string.Empty;
        public ProductionLine Line { get; set; } = null!;
        public int AttendanceCount { get; set; }
        public int AbsencesCount { get; set; }
    }

    internal class ParsedDepartment
    {
        public string AreaName { get; set; } = string.Empty;
        public string LineName { get; set; } = string.Empty;
    }
}
