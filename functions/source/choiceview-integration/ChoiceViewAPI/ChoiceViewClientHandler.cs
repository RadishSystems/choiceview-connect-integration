using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace ChoiceViewAPI
{
    public class ChoiceViewClientHandler : HttpClientHandler
    {
        private readonly ChoiceViewApiToken _apiToken;

        public string BearerToken => _apiToken.BearerToken;
        public DateTime TokenExpiration => _apiToken.TokenExpiration;

        public ChoiceViewClientHandler(string clientId, string clientSecret)
        {
            _apiToken = new ChoiceViewApiToken(clientId, clientSecret);
        }

        public async Task<string> GetToken()
        {
            return await _apiToken.GetToken();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // See if the request has an authorization header. if not, add a header with a valid API token
            var auth = request.Headers.Authorization;
            if (auth != null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(auth.Scheme, await _apiToken.GetToken());
            }

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}