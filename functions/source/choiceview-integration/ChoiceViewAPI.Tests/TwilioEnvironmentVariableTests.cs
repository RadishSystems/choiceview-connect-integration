using System;
using Xunit;

namespace ChoiceViewAPI.Tests
{
    public class TwilioEnvironmentVariableTests
    {
        static TwilioEnvironmentVariableTests()
        {
            Environment.SetEnvironmentVariable("TWILIO_ACCOUNTSID", "ACxxxxxxxxxxxxxx");
            Environment.SetEnvironmentVariable("TWILIO_AUTHTOKEN", "TestAuthTokenValue");
            Environment.SetEnvironmentVariable("TWILIO_PHONENUMBER", "+15005550006");
        }

        [Fact]
        public void NotValidIfNoEnvironmentVariablesSet()
        {
            try
            {
                Environment.SetEnvironmentVariable("TWILIO_ACCOUNTSID", null);
                Environment.SetEnvironmentVariable("TWILIO_AUTHTOKEN", null);
                Assert.Throws<Exception>(() => new TwilioApi());
            }
            finally
            {
                Environment.SetEnvironmentVariable("TWILIO_ACCOUNTSID", "ACxxxxxxxxxxxxxx");
                Environment.SetEnvironmentVariable("TWILIO_AUTHTOKEN", "TestAuthTokenValue");
            }
        }

        [Fact]
        public void NotValidIfAccountSidNotSet()
        {
            try
            {
                Environment.SetEnvironmentVariable("TWILIO_ACCOUNTSID", null);
                Assert.Throws<Exception>(() => new TwilioApi());
            }
            finally
            {
                Environment.SetEnvironmentVariable("TWILIO_ACCOUNTSID", "ACxxxxxxxxxxxxxx");
            }
        }

        [Fact]
        public void NotValidIfAuthTokenNotSet()
        {
            try
            {
                Environment.SetEnvironmentVariable("TWILIO_AUTHTOKEN", null);
                Assert.Throws<Exception>(() => new TwilioApi());
            }
            finally
            {
                Environment.SetEnvironmentVariable("TWILIO_AUTHTOKEN", "TestAuthTokenValue");
            }
        }

        [Fact]
        public void ValidIfEnvironmentVariablesSet()
        {
            var twilioApi = new TwilioApi();
            Assert.NotNull(twilioApi.LookupsApi);
            Assert.NotNull(twilioApi.MessagingApi);
            Assert.False(string.IsNullOrEmpty(twilioApi.AccountSid));
            Assert.False(string.IsNullOrEmpty(twilioApi.SmsNumber));
        }
    }
}

