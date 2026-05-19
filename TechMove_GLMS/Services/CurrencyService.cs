using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace TechMove_GLMS.Services
{
    public interface ICurrencyService
    {
        Task<decimal> ConvertToZarAsync(decimal foreignAmount, string currencyCode);
    }

    public class CurrencyService : ICurrencyService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public CurrencyService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        public async Task<decimal> ConvertToZarAsync(decimal foreignAmount, string currencyCode)
        {
            // If they are already using ZAR, no API call needed!
            if (currencyCode.ToUpper() == "ZAR") return foreignAmount;

            try
            {
                string apiKey = _config["ExternalApis:ExchangeRateApi"];

                if (string.IsNullOrEmpty(apiKey) || apiKey == "ENTER_KEY_HERE")
                {
                    throw new Exception($"DEBUG: The API key was not loaded! Value is currently: '{apiKey}'");
                }

                // Public exchange rate API
                string url = $"https://v6.exchangerate-api.com/v6/{apiKey}/latest/{currencyCode}";
                
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode(); // Throws an exception if the API is down (404, 500)

                var result = await response.Content.ReadFromJsonAsync<ExchangeRateResponse>();
                
                // Extract the ZAR rate and calculate
                decimal zarRate = result.Rates["ZAR"];
                return Math.Round(foreignAmount * zarRate, 2);
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"The live financial API is currently unreachable. Cannot convert {currencyCode} to ZAR.", ex);
            }
        }
        
        // Private class to map the JSON response
        // Explicitly map the lowercase JSON string to prevent parsing errors
        private class ExchangeRateResponse 
        { 
            [JsonPropertyName("conversion_rates")]
            public Dictionary<string, decimal> Rates { get; set; } 
        }
    }
}