using System;
using System.Threading.Tasks;
using Xunit;

namespace ChoiceViewAPI.Tests
{
    public class ChoiceViewApiTokenTests
    {
        private readonly string _clientId;
        private readonly string _clientSecret;

        public ChoiceViewApiTokenTests()
        {
            DotNetEnv.Env.Load("../../../.env");
            _clientId = Environment.GetEnvironmentVariable("CHOICEVIEW_CLIENTID");
            _clientSecret = Environment.GetEnvironmentVariable("CHOICEVIEW_CLIENTSECRET");
        }

        [Fact]
        public void ThrowsExceptionIfMissingClientIdinConstructor()
        {
            var exception = Record.Exception(() => new ChoiceViewApiToken(null, _clientSecret));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentNullException>(exception);
            Assert.Contains("clientId", exception.Message);
        }

        [Fact]
        public void ThrowsExceptionIfMissingClientSecretinConstructor()
        {
            var exception = Record.Exception(() => new ChoiceViewApiToken(_clientId, null));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentNullException>(exception);
            Assert.Contains("clientSecret", exception.Message);
        }

        [Fact]
        public void ThrowsExceptionIfBlankClientIdinConstructor()
        {
            var exception = Record.Exception(() => new ChoiceViewApiToken(" ", _clientSecret));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
            Assert.Contains("clientId", exception.Message);
        }

        [Fact]
        public void ThrowsExceptionIfBlankClientSecretinConstructor()
        {
            var exception = Record.Exception(() => new ChoiceViewApiToken(_clientId, " "));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
            Assert.Contains("clientSecret", exception.Message);
        }

        [Fact]
        public void CorrentPropertyInitializationOnCreation()
        {
            var lowerExpirationDate = DateTime.Now;
            var apiToken = new ChoiceViewApiToken(_clientId, _clientSecret);
            Assert.Null(apiToken.BearerToken);
            Assert.False(apiToken.TokenExpiration > DateTime.Now);
            Assert.InRange(apiToken.TokenExpiration, lowerExpirationDate, DateTime.Now);
        }

        [Fact]
        public async Task SucessfullyCreatesTokenOnFirstMethodCallAfterCreation()
        {
            var apiToken = new ChoiceViewApiToken(_clientId, _clientSecret);
            var token = await apiToken.GetToken();
            Assert.NotNull(token);
            Assert.NotNull(apiToken.BearerToken);
            Assert.True(apiToken.TokenExpiration > DateTime.Now);
        }
    }
}