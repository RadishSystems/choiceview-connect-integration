using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Xunit;

namespace ChoiceViewAPI.Tests
{
    public class ChoiceViewClientHandlerTests
    {
        private readonly string _clientId;
        private readonly string _clientSecret;

        public ChoiceViewClientHandlerTests()
        {
            DotNetEnv.Env.Load("../../../.env");
            _clientId = Environment.GetEnvironmentVariable("CHOICEVIEW_CLIENTID");
            _clientSecret = Environment.GetEnvironmentVariable("CHOICEVIEW_CLIENTSECRET");
        }

        [Fact]
        public void ThrowsExceptionIfMissingClientIdinConstructor()
        {
            var exception = Record.Exception(() => new ChoiceViewClientHandler(null, _clientSecret));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentNullException>(exception);
            Assert.Contains("clientId", exception.Message);
        }

        [Fact]
        public void ThrowsExceptionIfMissingClientSecretinConstructor()
        {
            var exception = Record.Exception(() => new ChoiceViewClientHandler(_clientId, null));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentNullException>(exception);
            Assert.Contains("clientSecret", exception.Message);
        }

        [Fact]
        public void ThrowsExceptionIfBlankClientIdinConstructor()
        {
            var exception = Record.Exception(() => new ChoiceViewClientHandler(" ", _clientSecret));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
            Assert.Contains("clientId", exception.Message);
        }

        [Fact]
        public void ThrowsExceptionIfBlankClientSecretinConstructor()
        {
            var exception = Record.Exception(() => new ChoiceViewClientHandler(_clientId, " "));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
            Assert.Contains("clientSecret", exception.Message);
        }

        [Fact]
        public void CorrentPropertyInitializationOnCreation()
        {
            var lowerExpirationDate = DateTime.Now;
            using (var clientHandler = new ChoiceViewClientHandler(_clientId, _clientSecret))
            {
                Assert.Null(clientHandler.BearerToken);
                Assert.False(clientHandler.TokenExpiration > DateTime.Now);
                Assert.InRange(clientHandler.TokenExpiration, lowerExpirationDate, DateTime.Now);
            }
        }

        [Fact]
        public async Task SucessfullyCreatesTokenOnFirstMethodCallAfterCreation()
        {
            using (var clientHandler = new ChoiceViewClientHandler(_clientId, _clientSecret))
            {
                var token = await clientHandler.GetToken();
                Assert.NotNull(token);
                Assert.NotNull(clientHandler.BearerToken);
                Assert.True(clientHandler.TokenExpiration > DateTime.Now);
            }
        }

        [Fact]
        public async Task SucessfullyCreatesTokenOnFirstRequestAfterCreation()
        {
            using (var httpClient = new HttpClient(new ChoiceViewClientHandler(_clientId, _clientSecret))
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.ConnectionClose = false;
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer");

                var response = await httpClient.DeleteAsync("session/1");

                Assert.NotNull(response);
                Assert.True(HttpStatusCode.NotFound == response.StatusCode);
            }
        }
    }
}