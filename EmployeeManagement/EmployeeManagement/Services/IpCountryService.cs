using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace EmployeeManagement.Services
{
    public class IpCountryService
    {
        private readonly HttpClient _http;
        private readonly ILogger<IpCountryService> _logger;

        public IpCountryService(HttpClient http, ILogger<IpCountryService> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<string?> GetCountryCodeAsync(string ipAddress)
        {
            try
            {
                var response = await _http.GetFromJsonAsync<IpApiResponse>(ipAddress);

                if (response is { Success: true })
                    return response.CountryCode;

                _logger.LogWarning("IP API nevrátilo krajinu pre adresu {Ip}", ipAddress);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chyba pri volaní IP API pre adresu {Ip}", ipAddress);
                return null;
            }
        }
    }

    public class IpApiResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("country_code")]
        public string? CountryCode { get; set; }
    }
}