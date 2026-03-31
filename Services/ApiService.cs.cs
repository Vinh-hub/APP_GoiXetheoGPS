using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;

namespace APP_GoixetheoGPS.Services
{
    public class ApiService
    {
        HttpClient client = new HttpClient();

        string baseUrl = "https://localhost:5000";
        private string _jwtToken;

        public async Task<string> Login(string username, string password)
        {
            var loginData = new { Username = username, Password = password };
            var json = JsonSerializer.Serialize(loginData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var res = await client.PostAsync($"{baseUrl}/api/auth/login", content);

            if (res.IsSuccessStatusCode)
            {
                var responseString = await res.Content.ReadAsStringAsync();
                var responseData = JsonSerializer.Deserialize<JsonElement>(responseString);
                
                if (responseData.TryGetProperty("token", out var tokenProp))
                {
                    _jwtToken = tokenProp.GetString();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);
                    return _jwtToken;
                }
            }
            
            return null;
        }

        public void Logout()
        {
            _jwtToken = null;
            client.DefaultRequestHeaders.Authorization = null;
        }

        public async Task<string> BookTrip(object trip)
        {
            var json = JsonSerializer.Serialize(trip);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var res = await client.PostAsync($"{baseUrl}/api/trips", content);

            return await res.Content.ReadAsStringAsync();
        }
    }
}