using Microsoft.AspNetCore.Mvc;

namespace GDIKPI.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PartNumberApiController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public PartNumberApiController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        [HttpGet("GetPartNumber")]
        public async Task<IActionResult> GetPartNumber([FromQuery] string partNumber)
        {
            try
            {
                if (string.IsNullOrEmpty(partNumber))
                {
                    return BadRequest("El parámetro partNumber es requerido");
                }

                // URL del API externo
                var externalApiUrl = $"http://192.168.1.1:9091/GetPartNumber?partNumber={Uri.EscapeDataString(partNumber)}";

                var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.GetAsync(externalApiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return NotFound($"No se encontró el número de parte: {partNumber}");
                    }
                    return StatusCode((int)response.StatusCode, "Error al consultar el API externo");
                }

                var data = await response.Content.ReadAsStringAsync();
                return Content(data, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al consultar el API externo: {ex.Message}");
            }
        }
    }
}
