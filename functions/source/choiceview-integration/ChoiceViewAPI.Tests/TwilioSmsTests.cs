using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.Lambda.TestUtilities;
using Moq;
using Newtonsoft.Json.Linq;
using Twilio;
using Xunit;
using Xunit.Abstractions;

namespace ChoiceViewAPI.Tests
{
    public class TwilioSmsTests
    {
        private readonly JObject _connectEvent;
        private readonly TestLambdaContext _context;
        private readonly string _systemNumber = "+18582016694";
        private readonly string _customerNumber = "+17202950840";
        private readonly string _smsNumber = "+15005550006";

        public TwilioSmsTests(ITestOutputHelper output)
        {
            _context = new TestLambdaContext
            {
                Logger = new XUnitLambaLogger(output)
            };

            _connectEvent = JObject.Parse(
@"{
    ""Details"": {
      ""ContactData"": {
        ""Attributes"": {},
        ""Channel"": ""VOICE"",
        ""ContactId"": """",
        ""CustomerEndpoint"": {
          ""Address"": ""+17202950840"",
          ""Type"": ""TELEPHONE_NUMBER""
        },
        ""InitialContactId"": """",
        ""InitiationMethod"": ""INBOUND"",
        ""InstanceARN"": """",
        ""PreviousContactId"": """",
        ""Queue"": null,
        ""SystemEndpoint"": {
          ""Address"": ""+18582016694"",
          ""Type"": ""TELEPHONE_NUMBER""
        }
      },
      ""Parameters"": {
        ""RequestName"": ""SendSms"",
        ""SmsMessage"": ""Tap this link to start ChoiceView: http://choiceview.com/start.html?account=radish1&phone=""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
        }

        [Fact]
        public async Task SmsReturnsFalseIfValidButBadCredentials()
        {
            var lookupsMock = new Mock<ITwilioLookupsApi>(MockBehavior.Strict);
            var lookupsException =
                await TwilioTests.CreateException($"https://lookups.twilio.com/v1/PhoneNumbers/{_customerNumber}?Type=carrier", HttpMethod.Get, HttpStatusCode.Unauthorized);
            lookupsMock.Setup(api => api.NumberInfo(_customerNumber, It.IsAny<string>(), "carrier"))
                .ThrowsAsync(lookupsException);

            var messagingMock = new Mock<ITwilioMessagingApi>(MockBehavior.Strict);

            var twilioApi = new TwilioApi(lookupsMock.Object, messagingMock.Object, "ACxxxxxxxxxxxxxx", _systemNumber);

            var connectFunction = new SmsWorkflow(twilioApi);
            var result = await connectFunction.Process(_connectEvent, _context);

            Assert.Single(result);
            var lambdaResult = result["LambdaResult"];
            Assert.NotNull(lambdaResult);
            Assert.True(lambdaResult.Type == JTokenType.Boolean);
            Assert.False(lambdaResult.Value<bool>());

            lookupsMock.VerifyAll();
        }

        [Fact]
        public async Task SmsReturnsTrueIfValidCredentialsAndMessageQueued()
        {
            var lookupsMock = new Mock<ITwilioLookupsApi>(MockBehavior.Strict);
            lookupsMock.Setup(api => api.NumberInfo(It.IsAny<string>(), It.IsAny<string>(), "carrier"))
                .ReturnsAsync((string phoneNumber, string countryCode, string type) =>
                    JObject.Parse($"{{\"url\": \"https://lookups.twilio.com/v1/PhoneNumbers/{phoneNumber}?Type=carrier\",\"carrier\": {{ \"type\": \"mobile\" }},\"phone_number\": \"{phoneNumber}\",\"country_code\": \"{countryCode}\"}}"));

            var smsBodyNumber = IVRWorkflow.SwitchCallerId(_customerNumber);
            var messagingMock = new Mock<ITwilioMessagingApi>(MockBehavior.Strict);
            messagingMock.Setup(api => api.SendSMS(It.IsAny<string>(), It.Is<Dictionary<string,string>>(
                args => args["From"].Equals(_systemNumber) && args["To"].Equals(_customerNumber) && args["Body"].Contains($"={smsBodyNumber}"))))
                .ReturnsAsync((string accountSid, Dictionary<string, string> args) =>
                    JObject.Parse($"{{\"account_sid\": \"{accountSid}\",\"api_version\": \"2010-04-01\",\"body\": \"{args["Body"]}\",\"from\": \"{args["From"]}\",\"status\": \"queued\",\"to\": \"{args["To"]}\"}}"));

            var twilioApi = new TwilioApi(lookupsMock.Object, messagingMock.Object, "ACxxxxxxxxxxxxxx", _systemNumber);

            var connectFunction = new SmsWorkflow(twilioApi);
            var result = await connectFunction.Process(_connectEvent, _context);

            Assert.Single(result);
            var lambdaResult = result["LambdaResult"];
            Assert.NotNull(lambdaResult);
            Assert.True(lambdaResult.Type == JTokenType.Boolean, "LambdaResult type is not Boolean");
            Assert.True(lambdaResult.Value<bool>(), "LambdaResult value is not true");

            lookupsMock.VerifyAll();
            messagingMock.VerifyAll();
        }

        [Fact]
        public async Task SmsReturnsTrueIfValidCredentialsAndMessageQueuedAndTwilioPhoneNumberSet()
        {
            var lookupsMock = new Mock<ITwilioLookupsApi>(MockBehavior.Strict);
            lookupsMock.Setup(api => api.NumberInfo(It.IsAny<string>(), It.IsAny<string>(), "carrier"))
                .ReturnsAsync((string phoneNumber, string countryCode, string type) =>
                    JObject.Parse($"{{\"url\": \"https://lookups.twilio.com/v1/PhoneNumbers/{phoneNumber}?Type=carrier\",\"carrier\": {{ \"type\": \"mobile\" }},\"phone_number\": \"{phoneNumber}\",\"country_code\": \"{countryCode}\"}}"));

            var smsBodyNumber = IVRWorkflow.SwitchCallerId(_customerNumber);
            var messagingMock = new Mock<ITwilioMessagingApi>(MockBehavior.Strict);
            messagingMock.Setup(api => api.SendSMS(It.IsAny<string>(), It.Is<Dictionary<string, string>>(
                    args => args["From"].Equals(_smsNumber) && args["To"].Equals(_customerNumber) && args["Body"].Contains($"={smsBodyNumber}"))))
                .ReturnsAsync((string accountSid, Dictionary<string, string> args) =>
                    JObject.Parse($"{{\"account_sid\": \"{accountSid}\",\"api_version\": \"2010-04-01\",\"body\": \"{args["Body"]}\",\"from\": \"{args["From"]}\",\"status\": \"queued\",\"to\": \"{args["To"]}\"}}"));

            var twilioApi = new TwilioApi(lookupsMock.Object, messagingMock.Object, "ACxxxxxxxxxxxxxx", _systemNumber);

            var connectFunction = new SmsWorkflow(twilioApi);
            try
            {
                Environment.SetEnvironmentVariable("TWILIO_PHONENUMBER", _smsNumber);
                var result = await connectFunction.Process(_connectEvent, _context);

                Assert.Single(result);
                var lambdaResult = result["LambdaResult"];
                Assert.NotNull(lambdaResult);
                Assert.True(lambdaResult.Type == JTokenType.Boolean, "LambdaResult type is not Boolean");
                Assert.True(lambdaResult.Value<bool>(), "LambdaResult value is not true");
            }
            finally
            {
                Environment.SetEnvironmentVariable("TWILIO_PHONENUMBER", null);
            }

            lookupsMock.VerifyAll();
            messagingMock.VerifyAll();
        }
    }
}
