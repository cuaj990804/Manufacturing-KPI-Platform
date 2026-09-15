using System.Text.Json;
using GDIKPI.Data;
using GDIKPI.Hubs;
using GDIKPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GDIKPI.ApiControllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardProductionVisibilityController : ControllerBase
    {
        private static readonly SemaphoreSlim FileLock = new(1, 1);
        private static readonly SemaphoreSlim TableLock = new(1, 1);
        private static bool _tableEnsured;
        private readonly KpisContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IHubContext<DashboardHub> _hubContext;
        private readonly ILogger<DashboardProductionVisibilityController> _logger;
        private readonly string _settingsPath;

        public DashboardProductionVisibilityController(
            KpisContext context,
            IWebHostEnvironment environment,
            IHubContext<DashboardHub> hubContext,
            ILogger<DashboardProductionVisibilityController> logger)
        {
            _context = context;
            _environment = environment;
            _hubContext = hubContext;
            _logger = logger;
            _settingsPath = ResolveSettingsPath();
            _logger.LogInformation("Settings path resolved to: {SettingsPath}", _settingsPath);
        }

        [HttpGet]
        public async Task<IActionResult> GetVisibility()
        {
            try
            {
                var settings = await ReadSettingsAsync();
                return Ok(new
                {
                    hiddenLineIds = settings.HiddenLineIds.OrderBy(id => id),
                    hiddenCardKeys = settings.HiddenCardKeys.OrderBy(key => key),
                    cardOrder = settings.CardOrder
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GetVisibility");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateVisibility([FromBody] DashboardLineVisibilityRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { error = "Los datos de visibilidad son requeridos" });
                }

                if (request.LineId <= 0)
                {
                    return BadRequest(new { error = "LineId invalido" });
                }

                var settings = await ReadSettingsAsync();

                if (request.IsVisible)
                {
                    settings.HiddenLineIds.Remove(request.LineId);
                }
                else
                {
                    settings.HiddenLineIds.Add(request.LineId);
                }

                await WriteSettingsAsync(settings);
                await NotifyDashboardsAsync();

                return Ok(new
                {
                    hiddenLineIds = settings.HiddenLineIds.OrderBy(id => id),
                    hiddenCardKeys = settings.HiddenCardKeys.OrderBy(key => key),
                    cardOrder = settings.CardOrder
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en UpdateVisibility");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        [HttpPost("bulk")]
        public async Task<IActionResult> UpdateBulkVisibility([FromBody] DashboardBulkVisibilityRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { error = "Los datos de visibilidad son requeridos" });
                }

                var lineIds = (request.LineIds ?? new List<int>())
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

                var settings = await ReadSettingsAsync();

                foreach (var lineId in lineIds)
                {
                    if (request.IsVisible)
                    {
                        settings.HiddenLineIds.Remove(lineId);
                    }
                    else
                    {
                        settings.HiddenLineIds.Add(lineId);
                    }
                }

                await WriteSettingsAsync(settings);
                await NotifyDashboardsAsync();

                return Ok(new
                {
                    hiddenLineIds = settings.HiddenLineIds.OrderBy(id => id),
                    hiddenCardKeys = settings.HiddenCardKeys.OrderBy(key => key),
                    cardOrder = settings.CardOrder
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en UpdateBulkVisibility");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        [HttpPut("card")]
        public async Task<IActionResult> UpdateCardVisibility([FromBody] DashboardCardVisibilityRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { error = "Los datos de visibilidad son requeridos" });
                }

                if (string.IsNullOrWhiteSpace(request.CardKey))
                {
                    return BadRequest(new { error = "CardKey invalido" });
                }

                var cardKey = request.CardKey.Trim();
                var settings = await ReadSettingsAsync();

                if (request.IsVisible)
                {
                    settings.HiddenCardKeys.Remove(cardKey);
                    RemoveLegacyLineId(settings, cardKey);
                }
                else
                {
                    settings.HiddenCardKeys.Add(cardKey);
                    AddLegacyLineId(settings, cardKey);
                }

                await WriteSettingsAsync(settings);
                await NotifyDashboardsAsync();

                return Ok(new
                {
                    hiddenLineIds = settings.HiddenLineIds.OrderBy(id => id),
                    hiddenCardKeys = settings.HiddenCardKeys.OrderBy(key => key),
                    cardOrder = settings.CardOrder
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en UpdateCardVisibility");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        [HttpPost("cards/bulk")]
        public async Task<IActionResult> UpdateBulkCardVisibility([FromBody] DashboardBulkCardVisibilityRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { error = "Los datos de visibilidad son requeridos" });
                }

                var cardKeys = (request.CardKeys ?? new List<string>())
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Select(key => key.Trim())
                    .Distinct()
                    .ToList();

                var settings = await ReadSettingsAsync();

                foreach (var cardKey in cardKeys)
                {
                    if (request.IsVisible)
                    {
                        settings.HiddenCardKeys.Remove(cardKey);
                        RemoveLegacyLineId(settings, cardKey);
                    }
                    else
                    {
                        settings.HiddenCardKeys.Add(cardKey);
                        AddLegacyLineId(settings, cardKey);
                    }
                }

                await WriteSettingsAsync(settings);
                await NotifyDashboardsAsync();

                return Ok(new
                {
                    hiddenLineIds = settings.HiddenLineIds.OrderBy(id => id),
                    hiddenCardKeys = settings.HiddenCardKeys.OrderBy(key => key),
                    cardOrder = settings.CardOrder
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en UpdateBulkCardVisibility");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        [HttpPut("cards/order")]
        public async Task<IActionResult> UpdateCardOrder([FromBody] DashboardCardOrderRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { error = "Los datos de orden son requeridos" });
                }

                var cardOrder = NormalizeCardOrder(request.CardOrder);

                var settings = await ReadSettingsAsync();
                settings.CardOrder = cardOrder;

                await WriteSettingsAsync(settings);
                await NotifyDashboardsAsync();

                return Ok(new
                {
                    hiddenLineIds = settings.HiddenLineIds.OrderBy(id => id),
                    hiddenCardKeys = settings.HiddenCardKeys.OrderBy(key => key),
                    cardOrder = settings.CardOrder
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en UpdateCardOrder");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        private async Task<DashboardVisibilitySettings> ReadSettingsAsync()
        {
            await EnsureSettingsTableAsync();

            var records = await _context.DashboardProductionCardSettings
                .AsNoTracking()
                .OrderBy(setting => setting.SortOrder == 0 ? int.MaxValue : setting.SortOrder)
                .ThenBy(setting => setting.DashboardProductionCardSettingId)
                .ToListAsync();

            if (records.Count == 0)
            {
                var legacySettings = await ReadLegacySettingsAsync();
                if (legacySettings.HiddenLineIds.Count > 0
                    || legacySettings.HiddenCardKeys.Count > 0
                    || legacySettings.CardOrder.Count > 0)
                {
                    await WriteSettingsAsync(legacySettings);
                    records = await _context.DashboardProductionCardSettings
                        .AsNoTracking()
                        .OrderBy(setting => setting.SortOrder == 0 ? int.MaxValue : setting.SortOrder)
                        .ThenBy(setting => setting.DashboardProductionCardSettingId)
                        .ToListAsync();
                }
            }

            return BuildSettingsFromRecords(records);
        }

        private async Task WriteSettingsAsync(DashboardVisibilitySettings settings)
        {
            await EnsureSettingsTableAsync();
            NormalizeSettings(settings);

            var desiredSettings = BuildCardSettings(settings);
            var existingSettings = await _context.DashboardProductionCardSettings.ToListAsync();

            _context.DashboardProductionCardSettings.RemoveRange(existingSettings);
            await _context.DashboardProductionCardSettings.AddRangeAsync(desiredSettings);
            await _context.SaveChangesAsync();
        }

        private async Task<DashboardVisibilitySettings> ReadLegacySettingsAsync()
        {
            await FileLock.WaitAsync();
            try
            {
                var filePath = GetSettingsPath();
                if (!System.IO.File.Exists(filePath))
                {
                    return new DashboardVisibilitySettings();
                }

                var json = await System.IO.File.ReadAllTextAsync(filePath);
                var settings = JsonSerializer.Deserialize<DashboardVisibilitySettings>(json)
                    ?? new DashboardVisibilitySettings();

                NormalizeSettings(settings);
                return settings;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo importar la configuracion legacy desde {Path}", GetSettingsPath());
                return new DashboardVisibilitySettings();
            }
            finally
            {
                FileLock.Release();
            }
        }

        private async Task EnsureSettingsTableAsync()
        {
            if (_tableEnsured)
            {
                return;
            }

            await TableLock.WaitAsync();
            try
            {
                if (_tableEnsured)
                {
                    return;
                }

                await _context.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[dbo].[DashboardProductionCardSettings]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DashboardProductionCardSettings]
    (
        [DashboardProductionCardSettingId] INT IDENTITY(1,1) NOT NULL,
        [CardKey] VARCHAR(255) NOT NULL,
        [IsVisible] BIT NOT NULL CONSTRAINT [DF_DashboardProductionCardSettings_IsVisible] DEFAULT(1),
        [SortOrder] INT NOT NULL CONSTRAINT [DF_DashboardProductionCardSettings_SortOrder] DEFAULT(0),
        [UpdatedAt] DATETIME2(0) NOT NULL CONSTRAINT [DF_DashboardProductionCardSettings_UpdatedAt] DEFAULT(SYSDATETIME()),
        CONSTRAINT [PK_DashboardProductionCardSettings] PRIMARY KEY CLUSTERED ([DashboardProductionCardSettingId] ASC)
    );

    CREATE UNIQUE INDEX [UX_DashboardProductionCardSettings_CardKey]
        ON [dbo].[DashboardProductionCardSettings] ([CardKey]);
END");

                _tableEnsured = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error asegurando tabla DashboardProductionCardSettings");
                throw;
            }
            finally
            {
                TableLock.Release();
            }
        }

        private string GetSettingsPath()
        {
            return _settingsPath;
        }

        private string ResolveSettingsPath()
        {
            var appDataPath = Path.Combine(
                _environment.ContentRootPath,
                "App_Data",
                "dashboard-production-visibility.json");

            if (CanWriteToSettingsPath(appDataPath))
            {
                _logger.LogDebug("Using App_Data path for settings: {Path}", appDataPath);
                return appDataPath;
            }

            _logger.LogWarning("Cannot write to App_Data directory: {Dir}. Falling back to Temp path.",
                Path.GetDirectoryName(appDataPath));

            var tempPath = Path.Combine(
                Path.GetTempPath(),
                "GDIKPI",
                "dashboard-production-visibility.json");

            if (CanWriteToSettingsPath(tempPath))
            {
                _logger.LogInformation("Using Temp path for settings: {Path}", tempPath);
                return tempPath;
            }

            _logger.LogError("Cannot write to Temp path either: {Dir}. Falling back to App_Data as last resort.",
                Path.GetDirectoryName(tempPath));

            return appDataPath;
        }

        private static bool CanWriteToSettingsPath(string filePath)
        {
            try
            {
                var directoryPath = Path.GetDirectoryName(filePath)!;
                Directory.CreateDirectory(directoryPath);

                if (System.IO.File.Exists(filePath))
                {
                    using var stream = System.IO.File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                    return stream.CanWrite;
                }

                var testPath = Path.Combine(directoryPath, $".write-test-{Guid.NewGuid():N}.tmp");
                System.IO.File.WriteAllText(testPath, string.Empty);
                System.IO.File.Delete(testPath);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private Task NotifyDashboardsAsync()
        {
            return _hubContext.Clients.All.SendAsync("DashboardCardVisibilityUpdated");
        }

        private static void AddLegacyLineId(DashboardVisibilitySettings settings, string cardKey)
        {
            if (TryGetLineId(cardKey, out var lineId))
            {
                settings.HiddenLineIds.Add(lineId);
            }
        }

        private static void RemoveLegacyLineId(DashboardVisibilitySettings settings, string cardKey)
        {
            if (TryGetLineId(cardKey, out var lineId))
            {
                settings.HiddenLineIds.Remove(lineId);
            }
        }

        private static bool TryGetLineId(string cardKey, out int lineId)
        {
            lineId = 0;
            return cardKey.StartsWith("line:", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(cardKey["line:".Length..], out lineId);
        }

        private static DashboardVisibilitySettings BuildSettingsFromRecords(
            IEnumerable<DashboardProductionCardSetting> records)
        {
            var settings = new DashboardVisibilitySettings
            {
                CardOrder = records
                    .Where(record => record.SortOrder > 0)
                    .OrderBy(record => record.SortOrder)
                    .ThenBy(record => record.DashboardProductionCardSettingId)
                    .Select(record => record.CardKey)
                    .ToList()
            };

            foreach (var record in records.Where(record => !record.IsVisible))
            {
                settings.HiddenCardKeys.Add(record.CardKey);
                AddLegacyLineId(settings, record.CardKey);
            }

            NormalizeSettings(settings);
            return settings;
        }

        private static List<DashboardProductionCardSetting> BuildCardSettings(DashboardVisibilitySettings settings)
        {
            var cards = new Dictionary<string, DashboardProductionCardSetting>(StringComparer.OrdinalIgnoreCase);
            var sortOrder = 1;

            foreach (var cardKey in settings.CardOrder)
            {
                cards[cardKey] = new DashboardProductionCardSetting
                {
                    CardKey = cardKey,
                    IsVisible = true,
                    SortOrder = sortOrder++,
                    UpdatedAt = DateTime.Now
                };
            }

            foreach (var cardKey in settings.HiddenCardKeys)
            {
                if (!cards.TryGetValue(cardKey, out var card))
                {
                    card = new DashboardProductionCardSetting
                    {
                        CardKey = cardKey,
                        SortOrder = 0
                    };
                    cards[cardKey] = card;
                }

                card.IsVisible = false;
                card.UpdatedAt = DateTime.Now;
            }

            foreach (var lineId in settings.HiddenLineIds)
            {
                var cardKey = $"line:{lineId}";
                if (!cards.TryGetValue(cardKey, out var card))
                {
                    card = new DashboardProductionCardSetting
                    {
                        CardKey = cardKey,
                        SortOrder = 0
                    };
                    cards[cardKey] = card;
                }

                card.IsVisible = false;
                card.UpdatedAt = DateTime.Now;
            }

            return cards.Values
                .OrderBy(card => card.SortOrder == 0 ? int.MaxValue : card.SortOrder)
                .ThenBy(card => card.CardKey)
                .ToList();
        }

        private static void NormalizeSettings(DashboardVisibilitySettings settings)
        {
            settings.HiddenLineIds ??= new HashSet<int>();
            settings.HiddenCardKeys = new HashSet<string>(
                settings.HiddenCardKeys ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            settings.CardOrder = NormalizeCardOrder(settings.CardOrder);
        }

        private static List<string> NormalizeCardOrder(IEnumerable<string>? cardOrder)
        {
            return (cardOrder ?? Enumerable.Empty<string>())
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public class DashboardLineVisibilityRequest
    {
        public int LineId { get; set; }

        public bool IsVisible { get; set; }
    }

    public class DashboardBulkVisibilityRequest
    {
        public List<int> LineIds { get; set; } = new();

        public bool IsVisible { get; set; }
    }

    public class DashboardCardVisibilityRequest
    {
        public string CardKey { get; set; } = string.Empty;

        public bool IsVisible { get; set; }
    }

    public class DashboardBulkCardVisibilityRequest
    {
        public List<string> CardKeys { get; set; } = new();

        public bool IsVisible { get; set; }
    }

    public class DashboardCardOrderRequest
    {
        public List<string>? CardOrder { get; set; } = new();
    }

    public class DashboardVisibilitySettings
    {
        public HashSet<int> HiddenLineIds { get; set; } = new();

        public HashSet<string> HiddenCardKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> CardOrder { get; set; } = new();
    }
}
