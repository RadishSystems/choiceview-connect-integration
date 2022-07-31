using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Refit;

namespace ChoiceViewAPI.Tests
{
    public static class TwilioTests
    {
        public static async Task<ApiException> Create404Exception(string url, HttpMethod method)
        {
            return await CreateException(url, method, HttpStatusCode.NotFound);
        }

        public static async Task<ApiException> CreateException(string url, HttpMethod method, HttpStatusCode statusCode,
            string jsonContent = null)
        {
            var response = new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = string.IsNullOrWhiteSpace(jsonContent) ?
                    null : new StringContent(jsonContent, Encoding.UTF8, "application/json"),
                RequestMessage = new HttpRequestMessage(method, url)
            };
            return await ApiException.Create(response.RequestMessage, method, response, new RefitSettings());
        }
    }
}