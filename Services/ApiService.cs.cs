using System.Text;
using System.Text.Json;

namespace APP_GoixetheoGPS.Services
{
    public class ApiService
    {
        HttpClient client = new HttpClient();

        string baseUrl = "https://localhost:5000";

        public async Task<string> BookTrip(object trip)
        {
            var json = JsonSerializer.Serialize(trip);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var res = await client.PostAsync($"{baseUrl}/api/trips", content);

            return await res.Content.ReadAsStringAsync();
        }
    }
}