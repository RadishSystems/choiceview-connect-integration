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
    public class TwilioLookupTests
    {
        private readonly JObject _connectEvent;
        private readonly TestLambdaContext _context;
        private readonly string _customerNumber = "+17202950840";
        private readonly string _smsNumber = "+15005550006";

        public TwilioLookupTests(ITestOutputHelper output)
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
          ""Address"": ""+17025346630"",
          ""Type"": ""TELEPHONE_NUMBER""
        }
      },
      ""Parameters"": {
        ""RequestName"": ""GetPhoneNumberType""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
        }

        [Fact]
        public async Task LookupReturnsEmptyNumberTypeIfValidButBadCredentials()
        {
            var lookupsMock = new Mock<ITwilioLookupsApi>(MockBehavior.Strict);
            var lookupsException =
                await TwilioTests.CreateException($"https://lookups.twilio.com/v1/PhoneNumbers/{_customerNumber}?Type=carrier", HttpMethod.Get, HttpStatusCode.Unauthorized);
            lookupsMock.Setup(api => api.NumberInfo(_customerNumber, It.IsAny<string>(), "carrier"))
                .ThrowsAsync(lookupsException);

            var messagingMock = new Mock<ITwilioMessagingApi>(MockBehavior.Strict);

            var twilioApi = new TwilioApi(lookupsMock.Object, messagingMock.Object, "ACxxxxxxxxxxxxxx", _smsNumber);

            var connectFunction = new LookupWorkflow(twilioApi);
            var result = await connectFunction.Process(_connectEvent, _context);

            Assert.Equal(2, result.Count);

            var lambdaResult = result["LambdaResult"];
            Assert.NotNull(lambdaResult);
            Assert.True(lambdaResult.Type == JTokenType.Boolean);
            Assert.True(lambdaResult.Value<bool>());

            var numberType = result["NumberType"];
            Assert.NotNull(numberType);
            Assert.True(numberType.Type == JTokenType.String);
            var typeValue = (string)numberType;
            Assert.NotNull(typeValue);
            Assert.Equal(string.Empty, typeValue);
        }

        [Fact]
        public async Task LookupReturnsNumberTypeIfValidCredentials()
        {
            var lookupsMock = new Mock<ITwilioLookupsApi>(MockBehavior.Strict);
            lookupsMock.Setup(api => api.NumberInfo(It.IsAny<string>(), It.IsAny<string>(), "carrier"))
                .ReturnsAsync((string phoneNumber, string countryCode, string type) =>
                    JObject.Parse($"{{\"url\": \"https://lookups.twilio.com/v1/PhoneNumbers/{phoneNumber}?Type=carrier\",\"carrier\": {{ \"type\": \"mobile\" }},\"phone_number\": \"{phoneNumber}\",\"country_code\": \"{countryCode}\"}}"));

            var messagingMock = new Mock<ITwilioMessagingApi>(MockBehavior.Strict);

            var twilioApi = new TwilioApi(lookupsMock.Object, messagingMock.Object, "ACxxxxxxxxxxxxxx", _smsNumber);

            var connectFunction = new LookupWorkflow(twilioApi);
            var result = await connectFunction.Process(_connectEvent, _context);

            Assert.Equal(2, result.Count);

            var lambdaResult = result["LambdaResult"];
            Assert.NotNull(lambdaResult);
            Assert.True(lambdaResult.Type == JTokenType.Boolean);
            Assert.True(lambdaResult.Value<bool>());

            var numberType = result["NumberType"];
            Assert.NotNull(numberType);
            Assert.True(numberType.Type == JTokenType.String);
            var typeValue = (string)numberType;
            Assert.NotNull(typeValue);
            Assert.Equal("mobile", typeValue);

            lookupsMock.VerifyAll();
        }
    }
}
