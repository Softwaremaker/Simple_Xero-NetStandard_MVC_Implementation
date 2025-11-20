namespace XeroNetStandardApp.Services
{
    using System;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Xero.NetStandard.OAuth2.Model.Accounting;
    using XeroNetStandardApp.Controllers;

    public class OsmGeocoder : IDisposable
    {
        private readonly HttpClient _httpClient;

        public OsmGeocoder()
        {
            _httpClient = new HttpClient();
            // Nominatim requires a valid User-Agent with contact info
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("YourAppName/1.0 (your.email@example.com)");
        }

        /// <summary>
        /// Geocode a Xero Address and return "LAT,LON" (comma separated) or null if not found.
        /// </summary>
        public async Task<string?> GeocodeXeroAddressAsync(Address addr)
        {
            if (addr == null) return null;

            var queryAddress = XeroAddressHelper.NormalizeForGeocode(addr);
            if (string.IsNullOrWhiteSpace(queryAddress)) return null;

            // Nominatim search endpoint
            var url =
                $"https://nominatim.openstreetmap.org/search?format=json&limit=1&q={Uri.EscapeDataString(queryAddress)}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                // up to you: log response.StatusCode
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            var root = doc.RootElement;
            if (root.GetArrayLength() == 0)
            {
                // no result
                return null;
            }

            var first = root[0];
            var latString = first.GetProperty("lat").GetString();
            var lonString = first.GetProperty("lon").GetString();

            if (!double.TryParse(latString, out var lat)) return null;
            if (!double.TryParse(lonString, out var lon)) return null;

            // You asked for "LAT LON" as a simple comma-limited string:
            // e.g. "13.7563,100.5018"
            return $"{lat},{lon}";
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }

    public class GeocodedAddressResult
    {
        public Address Address { get; set; } = null!;
        public string LatLon { get; set; } = string.Empty; // "LAT,LON"
    }
}