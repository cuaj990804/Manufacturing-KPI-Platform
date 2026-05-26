using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;

namespace GDIKPI.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AttendanceDashboardApiController : ControllerBase
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly string _attendanceApiBaseUrl;
        private readonly int _timeoutSeconds;

        public AttendanceDashboardApiController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _attendanceApiBaseUrl = (_configuration["AttendanceApi:BaseUrl"] ?? "https://localhost:713").TrimEnd('/');
            _timeoutSeconds = int.TryParse(_configuration["AttendanceApi:Timeout"], out var timeout) ? timeout : 30;
        }

        [HttpGet("departments")]
        public async Task<IActionResult> GetDepartments()
        {
            try
            {
                var node = await GetJsonNodeAsync("api/Attendance/departments", null);
                var values = ExtractStringList(node);
                return Ok(values);
            }
            catch (ExternalApiException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpGet("employees/active")]
        public async Task<IActionResult> GetActiveEmployees([FromQuery] string? departamento)
        {
            if (string.IsNullOrWhiteSpace(departamento))
            {
                return Ok(new List<EmployeeSuggestionViewModel>());
            }

            try
            {
                var query = new Dictionary<string, string?>();
                query["departamento"] = departamento;

                var node = await GetJsonNodeAsync("api/Attendance/employees/active", query);
                var rows = ExtractArray(node)
                    .Select(ToEmployeeSuggestion)
                    .Where(x => !string.IsNullOrWhiteSpace(x.Label))
                    .ToList();

                return Ok(rows);
            }
            catch (ExternalApiException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch
            {
                return Ok(new List<EmployeeSuggestionViewModel>());
            }
        }

        [HttpPost("summary")]
        public async Task<IActionResult> GetSummary([FromBody] AttendanceFilterRequest? filters)
        {
            try
            {
                filters ??= new AttendanceFilterRequest();

                var historyTask = GetAttendancesAsync(filters);
                var absencesTask = GetAbsencesAsync(filters);
                await Task.WhenAll(historyTask, absencesTask);

                var history = historyTask.Result;
                var absences = absencesTask.Result;

                var summary = new
                {
                    totalRecords = history.Count,
                    uniqueEmployees = history
                        .Select(r => !string.IsNullOrWhiteSpace(r.EmployeeNumber) ? r.EmployeeNumber : r.EmployeeId?.ToString())
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),
                    departments = history
                        .Select(r => r.Department)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),
                    absences = absences.Count
                };

                return Ok(summary);
            }
            catch (ExternalApiException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("comparison-chart")]
        public async Task<IActionResult> GetComparisonChart([FromBody] AttendanceFilterRequest? filters)
        {
            try
            {
                filters ??= new AttendanceFilterRequest();
                Task<AttendanceComparisonTotals> comparisonTotalsTask;

                if (!string.IsNullOrWhiteSpace(filters.EmployeeIdFilter))
                {
                    comparisonTotalsTask = GetComparisonTotalsFromHistoryAsync(filters);
                }
                else
                {
                    comparisonTotalsTask = GetComparisonTotalsFromDepartmentReportAsync(filters);
                }

                var totals = await comparisonTotalsTask;

                return Ok(new
                {
                    labels = new[] { "Asistencias", "Ausencias" },
                    totals = new[] { totals.Attendances, totals.Absences },
                    chartTitle = "Asistencias vs ausencias"
                });
            }
            catch (ExternalApiException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("history-table")]
        public async Task<IActionResult> GetHistoryTable([FromForm] DataTableRequest request)
        {
            try
            {
                var filters = request.ToFilters();
                var history = await GetAttendancesAsync(filters);

                var filtered = ApplyHistoryOrdering(history, request).ToList();
                var page = filtered.Skip(request.Start).Take(request.Length > 0 ? request.Length : 10).ToList();

                return Ok(new
                {
                    draw = request.Draw,
                    recordsTotal = history.Count,
                    recordsFiltered = filtered.Count,
                    data = page
                });
            }
            catch (ExternalApiException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("absences-table")]
        public async Task<IActionResult> GetAbsencesTable([FromForm] DataTableRequest request)
        {
            try
            {
                var filters = request.ToFilters();
                var absences = await GetAbsencesAsync(filters);

                var filtered = ApplyAbsencesOrdering(absences, request).ToList();
                var page = filtered.Skip(request.Start).Take(request.Length > 0 ? request.Length : 10).ToList();

                return Ok(new
                {
                    draw = request.Draw,
                    recordsTotal = absences.Count,
                    recordsFiltered = filtered.Count,
                    data = page
                });
            }
            catch (ExternalApiException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        private async Task<List<AttendanceRecordViewModel>> GetAttendancesAsync(AttendanceFilterRequest filters)
        {
            var query = new Dictionary<string, string?>();

            if (int.TryParse(filters.EmployeeIdFilter, out var employeeId))
            {
                query["employeeId"] = employeeId.ToString(CultureInfo.InvariantCulture);
            }

            query["departamento"] = NullIfWhiteSpace(filters.DepartmentFilter);
            query["start"] = NormalizeDate(filters.StartDateFilter)?.ToString("yyyy-MM-dd");
            query["end"] = NormalizeDate(filters.EndDateFilter)?.ToString("yyyy-MM-dd");
            query["startTime"] = NullIfWhiteSpace(filters.StartTimeFilter);
            query["endTime"] = NullIfWhiteSpace(filters.EndTimeFilter);

            var node = await GetJsonNodeAsync("api/Attendance/history", query);
            return ExtractArray(node).Select(ToAttendanceRecord).ToList();
        }

        private async Task<AttendanceComparisonTotals> GetComparisonTotalsFromHistoryAsync(AttendanceFilterRequest filters)
        {
            var history = await GetAttendancesAsync(filters);
            var absences = await GetAbsencesAsync(filters);

            return new AttendanceComparisonTotals
            {
                Attendances = history.Count,
                Absences = absences.Count
            };
        }

        private async Task<AttendanceComparisonTotals> GetComparisonTotalsFromDepartmentReportAsync(AttendanceFilterRequest filters)
        {
            var query = new Dictionary<string, string?>
            {
                ["start"] = NormalizeDate(filters.StartDateFilter)?.ToString("yyyy-MM-dd"),
                ["end"] = NormalizeDate(filters.EndDateFilter)?.ToString("yyyy-MM-dd"),
                ["departamento"] = NullIfWhiteSpace(filters.DepartmentFilter)
            };

            var node = await GetJsonNodeAsync("api/Attendance/report/by-department", query);
            var rows = ExtractArray(node);

            if (rows.Count == 0)
            {
                return new AttendanceComparisonTotals();
            }

            return new AttendanceComparisonTotals
            {
                Attendances = rows.Sum(row => FirstIntNullable(row,
                    "asistieron",
                    "attendances",
                    "totalAsistencias",
                    "count",
                    "records",
                    "cantidad") ?? 0),
                Absences = rows.Sum(row => FirstIntNullable(row,
                    "faltaron",
                    "absences",
                    "totalFaltaron",
                    "missing") ?? 0)
            };
        }

        private async Task<List<AbsenceRecordViewModel>> GetAbsencesAsync(AttendanceFilterRequest filters)
        {
            var start = NormalizeDate(filters.StartDateFilter) ?? DateTime.Today;
            var end = NormalizeDate(filters.EndDateFilter) ?? start;

            var query = new Dictionary<string, string?>
            {
                ["start"] = start.ToString("yyyy-MM-dd"),
                ["end"] = end.ToString("yyyy-MM-dd"),
                ["departamento"] = NullIfWhiteSpace(filters.DepartmentFilter)
            };

            if (int.TryParse(filters.EmployeeIdFilter, out var employeeId))
            {
                query["employeeId"] = employeeId.ToString(CultureInfo.InvariantCulture);
            }

            var node = await GetJsonNodeAsync("api/Attendance/absences", query);
            return ExtractArray(node).Select(ToAbsenceRecord).ToList();
        }

        private IEnumerable<AttendanceRecordViewModel> ApplyHistoryOrdering(IEnumerable<AttendanceRecordViewModel> source, DataTableRequest request)
        {
            var sortColumn = request.Columns.ElementAtOrDefault(request.Order.FirstOrDefault()?.Column ?? 0)?.Data ?? "dateLabel";
            var sortDirection = request.Order.FirstOrDefault()?.Dir ?? "desc";

            Func<AttendanceRecordViewModel, object?> selector = sortColumn switch
            {
                "employeeId" => row => row.EmployeeId,
                "employeeNumber" => row => row.EmployeeNumber,
                "fullName" => row => row.FullName,
                "department" => row => row.Department,
                "timeLabel" => row => row.TimeLabel,
                _ => row => row.Timestamp ?? ParseDateOnlyOrMin(row.DateLabel)
            };

            return sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
                ? source.OrderBy(selector)
                : source.OrderByDescending(selector);
        }

        private IEnumerable<AbsenceRecordViewModel> ApplyAbsencesOrdering(IEnumerable<AbsenceRecordViewModel> source, DataTableRequest request)
        {
            var sortColumn = request.Columns.ElementAtOrDefault(request.Order.FirstOrDefault()?.Column ?? 0)?.Data ?? "dateLabel";
            var sortDirection = request.Order.FirstOrDefault()?.Dir ?? "desc";

            Func<AbsenceRecordViewModel, object?> selector = sortColumn switch
            {
                "employeeId" => row => row.EmployeeId,
                "employeeNumber" => row => row.EmployeeNumber,
                "fullName" => row => row.FullName,
                "department" => row => row.Department,
                "reason" => row => row.Reason,
                _ => row => ParseDateOnlyOrMin(row.DateLabel)
            };

            return sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
                ? source.OrderBy(selector)
                : source.OrderByDescending(selector);
        }

        private AttendanceRecordViewModel ToAttendanceRecord(JsonNode? row)
        {
            var timestamp = FirstDateTime(row,
                "timestamp",
                "fechaHora",
                "dateTime",
                "attendanceDateTime",
                "checkIn",
                "entryTime");

            var date = FirstDateOnly(row,
                "fecha",
                "date",
                "attendanceDate");

            var time = FirstTimeSpan(row,
                "hora",
                "time",
                "attendanceTime");

            if (!timestamp.HasValue && date.HasValue)
            {
                timestamp = date.Value.ToDateTime(time ?? TimeOnly.MinValue);
            }

            return new AttendanceRecordViewModel
            {
                EmployeeId = FirstIntNullable(row, "employeeId", "empleadoId", "idEmpleado", "id"),
                EmployeeNumber = FirstString(row, "employeeNumber", "numeroEmpleado", "employeeCode", "badge"),
                FullName = FirstString(row, "fullName", "nombreCompleto", "employeeName", "nombre", "name") ?? string.Empty,
                Department = FirstString(row, "departamento", "department", "area") ?? string.Empty,
                DateLabel = timestamp?.ToString("yyyy-MM-dd") ?? date?.ToString("yyyy-MM-dd") ?? string.Empty,
                TimeLabel = timestamp?.ToString("HH:mm") ?? time?.ToString(@"hh\:mm") ?? string.Empty,
                Timestamp = timestamp
            };
        }

        private AbsenceRecordViewModel ToAbsenceRecord(JsonNode? row)
        {
            var date = FirstDateOnly(row, "fecha", "date", "absenceDate");
            return new AbsenceRecordViewModel
            {
                EmployeeId = FirstIntNullable(row, "employeeId", "empleadoId", "idEmpleado", "id"),
                EmployeeNumber = FirstString(row, "employeeNumber", "numeroEmpleado", "employeeCode", "badge"),
                FullName = FirstString(row, "fullName", "nombreCompleto", "employeeName", "nombre", "name") ?? string.Empty,
                Department = FirstString(row, "departamento", "department", "area") ?? string.Empty,
                DateLabel = date?.ToString("yyyy-MM-dd") ?? FirstString(row, "fecha", "date", "absenceDate") ?? string.Empty,
                Reason = FirstString(row, "reason", "motivo", "status", "comentario") ?? string.Empty
            };
        }

        private EmployeeSuggestionViewModel ToEmployeeSuggestion(JsonNode? row)
        {
            var employeeId = FirstIntNullable(row, "employeeId", "empleadoId", "idEmpleado", "id");
            var employeeNumber = FirstString(row, "employeeNumber", "numeroEmpleado", "employeeCode", "badge");
            var fullName = FirstString(row, "fullName", "nombreCompleto", "employeeName", "nombre", "name") ?? string.Empty;
            var department = FirstString(row, "departamento", "department", "area") ?? string.Empty;

            var pieces = new List<string>();
            if (!string.IsNullOrWhiteSpace(employeeNumber))
            {
                pieces.Add(employeeNumber);
            }
            else if (employeeId.HasValue)
            {
                pieces.Add(employeeId.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (!string.IsNullOrWhiteSpace(fullName))
            {
                pieces.Add(fullName);
            }

            if (!string.IsNullOrWhiteSpace(department))
            {
                pieces.Add($"({department})");
            }

            return new EmployeeSuggestionViewModel
            {
                EmployeeId = employeeId,
                EmployeeNumber = employeeNumber,
                FullName = fullName,
                Department = department,
                Label = string.Join(" - ", pieces.Take(2)) + (pieces.Count > 2 ? $" {pieces[2]}" : string.Empty)
            };
        }

        private async Task<JsonNode?> GetJsonNodeAsync(string path, IDictionary<string, string?>? query)
        {
            var client = _httpClientFactory.CreateClient("AttendanceApi");
            client.Timeout = TimeSpan.FromSeconds(_timeoutSeconds);

            var url = BuildUrl(path, query);
            using var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var message = TryExtractErrorMessage(content) ?? $"Error al consultar el API de asistencia ({(int)response.StatusCode})";
                throw new ExternalApiException(message, (int)response.StatusCode);
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            try
            {
                return JsonNode.Parse(content);
            }
            catch (JsonException)
            {
                throw new ExternalApiException("El API de asistencia devolvio una respuesta no valida.", StatusCodes.Status502BadGateway);
            }
        }

        private string BuildUrl(string path, IDictionary<string, string?>? query)
        {
            var builder = new StringBuilder($"{_attendanceApiBaseUrl}/{path.TrimStart('/')}");
            var validQuery = (query ?? new Dictionary<string, string?>())
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

        private static List<string> ExtractStringList(JsonNode? node)
        {
            if (node is JsonArray array)
            {
                return array
                    .Select(item => item?.GetValue<string>())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item)
                    .ToList()!;
            }

            return ExtractArray(node)
                .Select(item => FirstString(item, "departamento", "department", "name", "label", "value"))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item)
                .ToList()!;
        }

        private static string? FirstString(JsonNode? node, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                if (TryGetProperty(node, propertyName) is JsonNode value)
                {
                    var raw = value.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(raw) && !raw.Equals("null", StringComparison.OrdinalIgnoreCase))
                    {
                        return raw;
                    }
                }
            }

            return null;
        }

        private static int FirstInt(JsonNode? node, params string[] propertyNames)
            => FirstIntNullable(node, propertyNames) ?? 0;

        private static int? FirstIntNullable(JsonNode? node, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                if (TryGetProperty(node, propertyName) is JsonNode value)
                {
                    if (value is JsonValue jsonValue)
                    {
                        if (jsonValue.TryGetValue<int>(out var intValue))
                        {
                            return intValue;
                        }

                        if (jsonValue.TryGetValue<long>(out var longValue))
                        {
                            return (int)longValue;
                        }
                    }

                    if (int.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                    {
                        return parsed;
                    }
                }
            }

            return null;
        }

        private static DateTime? FirstDateTime(JsonNode? node, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                if (TryGetProperty(node, propertyName) is JsonNode value &&
                    DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        private static DateOnly? FirstDateOnly(JsonNode? node, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                if (TryGetProperty(node, propertyName) is JsonNode value)
                {
                    if (DateOnly.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    {
                        return parsed;
                    }

                    if (DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dateTime))
                    {
                        return DateOnly.FromDateTime(dateTime);
                    }
                }
            }

            return null;
        }

        private static TimeOnly? FirstTimeSpan(JsonNode? node, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                if (TryGetProperty(node, propertyName) is JsonNode value)
                {
                    var raw = value.ToString();
                    if (TimeOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    {
                        return parsed;
                    }

                    if (TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out var span))
                    {
                        return TimeOnly.FromTimeSpan(span);
                    }
                }
            }

            return null;
        }

        private static JsonNode? TryGetProperty(JsonNode? node, string propertyName)
        {
            if (node is not JsonObject obj)
            {
                return null;
            }

            foreach (var property in obj)
            {
                if (property.Key.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value;
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
            catch (JsonException)
            {
                return content.Length <= 180 ? content : content[..180];
            }
        }

        private static string? NullIfWhiteSpace(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static DateTime? NormalizeDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
                ? parsed.Date
                : null;
        }

        private static DateTime ParseDateOnlyOrMin(string? value)
        {
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
            {
                return parsed;
            }

            return DateTime.MinValue;
        }

        public sealed class AttendanceFilterRequest
        {
            public string? DepartmentFilter { get; set; }
            public string? EmployeeIdFilter { get; set; }
            public string? StartDateFilter { get; set; }
            public string? EndDateFilter { get; set; }
            public string? StartTimeFilter { get; set; }
            public string? EndTimeFilter { get; set; }
        }

        public sealed class DataTableRequest
        {
            public int Draw { get; set; }
            public int Start { get; set; }
            public int Length { get; set; } = 10;
            public string? DepartmentFilter { get; set; }
            public string? EmployeeIdFilter { get; set; }
            public string? StartDateFilter { get; set; }
            public string? EndDateFilter { get; set; }
            public string? StartTimeFilter { get; set; }
            public string? EndTimeFilter { get; set; }
            public List<DataTableOrderRequest> Order { get; set; } = new();
            public List<DataTableColumnRequest> Columns { get; set; } = new();

            public AttendanceFilterRequest ToFilters()
            {
                return new AttendanceFilterRequest
                {
                    DepartmentFilter = DepartmentFilter,
                    EmployeeIdFilter = EmployeeIdFilter,
                    StartDateFilter = StartDateFilter,
                    EndDateFilter = EndDateFilter,
                    StartTimeFilter = StartTimeFilter,
                    EndTimeFilter = EndTimeFilter
                };
            }
        }

        public sealed class DataTableOrderRequest
        {
            public int Column { get; set; }
            public string? Dir { get; set; }
        }

        public sealed class DataTableColumnRequest
        {
            public string? Data { get; set; }
        }

        public sealed class AttendanceRecordViewModel
        {
            public int? EmployeeId { get; set; }
            public string? EmployeeNumber { get; set; }
            public string FullName { get; set; } = string.Empty;
            public string Department { get; set; } = string.Empty;
            public string DateLabel { get; set; } = string.Empty;
            public string TimeLabel { get; set; } = string.Empty;
            public DateTime? Timestamp { get; set; }
        }

        public sealed class AbsenceRecordViewModel
        {
            public int? EmployeeId { get; set; }
            public string? EmployeeNumber { get; set; }
            public string FullName { get; set; } = string.Empty;
            public string Department { get; set; } = string.Empty;
            public string DateLabel { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
        }

        public sealed class EmployeeSuggestionViewModel
        {
            public int? EmployeeId { get; set; }
            public string? EmployeeNumber { get; set; }
            public string FullName { get; set; } = string.Empty;
            public string Department { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;
        }

        private sealed class AttendanceComparisonTotals
        {
            public int Attendances { get; set; }
            public int Absences { get; set; }
        }

        private sealed class ExternalApiException : Exception
        {
            public int StatusCode { get; }

            public ExternalApiException(string message, int statusCode) : base(message)
            {
                StatusCode = statusCode;
            }
        }
    }
}
