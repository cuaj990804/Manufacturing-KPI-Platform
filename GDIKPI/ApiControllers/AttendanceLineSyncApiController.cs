using GDIKPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GDIKPI.ApiControllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AttendanceLineSyncApiController : ControllerBase
    {
        private readonly AttendanceLineSyncService _attendanceLineSyncService;

        public AttendanceLineSyncApiController(AttendanceLineSyncService attendanceLineSyncService)
        {
            _attendanceLineSyncService = attendanceLineSyncService;
        }

        [HttpGet("mappings")]
        public IActionResult GetMappings()
        {
            return Ok(_attendanceLineSyncService.GetMappings());
        }

        [HttpPost("sync")]
        public async Task<IActionResult> Sync([FromBody] AttendanceLineSyncRequest? request, CancellationToken cancellationToken)
        {
            var targetDate = DateOnly.TryParse(request?.Date, out var parsedDate)
                ? parsedDate
                : (DateOnly?)null;

            var result = await _attendanceLineSyncService.SyncAsync(targetDate, request?.ProductionLineId, cancellationToken);
            return Ok(result);
        }
    }

    public class AttendanceLineSyncRequest
    {
        public string? Date { get; set; }
        public int? ProductionLineId { get; set; }
    }
}
