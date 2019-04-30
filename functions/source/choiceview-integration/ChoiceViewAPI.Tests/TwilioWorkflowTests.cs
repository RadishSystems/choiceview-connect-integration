using System;
using Refit;
using Xunit;

namespace ChoiceViewAPI.Tests
{
    public class TwilioWorkflowTests
    {
        public TwilioWorkflowTests()
        {
            Environment.SetEnvironmentVariable("TWILIO_ACCOUNTSID", "ACxxxxxxxxxxxxxx");
            Environment.SetEnvironmentVariable("TWILIO_AUTHTOKEN", "TestAuthTokenValue");
        }

        [Fact]
        public void ThrowsExceptionIfAuthTokenNotSet()
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
    }
}