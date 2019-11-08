using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Weather.Model;

namespace Weather.Workers
{
    public class WeatherWorker : BackgroundService
    {
        private readonly ILogger<WeatherWorker> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;

        public WeatherWorker(ILogger<WeatherWorker> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _cache = cache;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    DateTimeOffset? expiresHeader = null;

                    var token = _configuration["accuweathertoken"];

                    if(!string.IsNullOrWhiteSpace(token))
                    {
                        var client = _httpClientFactory.CreateClient("AccuWeather");

                        var response = await client.GetAsync($"{_configuration["weather:uri"]}/348735?apikey={_configuration["accuweathertoken"]}&details=true", stoppingToken);

                        var model = await JsonSerializer.DeserializeAsync<Forecast[]>(await response.Content.ReadAsStreamAsync(), cancellationToken: stoppingToken);

                        _cache.Set(Constants.LATEST_FORECAST_CACHE_KEY, model.First());

                        expiresHeader = response.Content.Headers.Expires;
                    }
                    else
                    {
                        Console.WriteLine("No accuweather key set, returning mock data.");
                    }

                    //Honoring the expires time
                    if (expiresHeader.HasValue)
                    {
                       await Task.Delay(expiresHeader.Value.UtcDateTime.Subtract(DateTime.UtcNow), stoppingToken);
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromMinutes(60), stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Unexpected error fetching weather data: {ex}", ex);
                }
            }
        }
    }
}
