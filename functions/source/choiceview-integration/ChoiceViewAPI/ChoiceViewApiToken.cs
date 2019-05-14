using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ChoiceViewAPI
{
    public class ChoiceViewApiToken
    {
        private readonly Uri _tokenRequestUri = new Uri("https://radishsystems.auth0.com/oauth/token");
        private readonly JObject _tokenRequestBody;
        private readonly TimeSpan _tokenExpiration = TimeSpan.FromSeconds(86400);

        public string BearerToken { get; protected set; }
        public DateTime TokenExpiration { get; protected set; }

        public ChoiceViewApiToken(string clientId, string clientSecret)
        {
            if(clientId == null) throw new ArgumentNullException(nameof(clientId));
            if (clientSecret == null) throw new ArgumentNullException(nameof(clientSecret));
            if (string.IsNullOrWhiteSpace(clientId)) throw new ArgumentException("Invalid value", nameof(clientId));
            if (string.IsNullOrWhiteSpace(clientSecret)) throw new ArgumentException("Invalid value", nameof(clientSecret));

            TokenExpiration = DateTime.Now;
            _tokenRequestBody = new JObject(
                new JProperty("client_id", clientId), new JProperty("client_secret", clientSecret),
                new JProperty("audience", "https://radishsystems.com/ivr/api/"),
                new JProperty("grant_type", "client_credentials"));
        }

        public async Task<string> GetToken()
        {
            // Get new token if expired
            if (DateTime.Now > TokenExpiration)
            {
                BearerToken = await GetNewBearerToken();
            }
            return BearerToken;
        }

        private async Task<string> GetNewBearerToken()
        {
            TokenExpiration = DateTime.Now;
            BearerToken = string.Empty;

            using (var client = new HttpClient())
            {
                var response = await client.PostAsync(_tokenRequestUri,
                    new StringContent(_tokenRequestBody.ToString(), Encoding.UTF8, "application/json"));
                response.EnsureSuccessStatusCode();

                TokenExpiration += _tokenExpiration;
                return JObject.Parse(await response.Content.ReadAsStringAsync()).Value<string>("access_token");
            }
        }
    }
}