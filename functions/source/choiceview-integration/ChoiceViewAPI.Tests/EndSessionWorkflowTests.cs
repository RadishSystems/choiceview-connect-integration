using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.TestUtilities;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ChoiceViewAPI.Tests
{
    public class SuccessfullyEndSession : HttpClientHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    public class FailToEndSession : HttpClientHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var errorResponse = new JObject(new JProperty("message", "Not found - Cannot find session."));

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(errorResponse.ToString(), Encoding.UTF8, "application/json")
            };
        }
    }

    public class EndSessionWorkflowTests
    {
        public static string SessionUri = "session/10001";

        private readonly JObject _connectEvent;
        private readonly TestLambdaContext _context;

        public EndSessionWorkflowTests(ITestOutputHelper output)
        {
            _context = new TestLambdaContext
            {
                Logger = new XUnitLambaLogger(output)
            };
            _connectEvent = JObject.Parse(
                @"{
    ""Details"": {
      ""ContactData"": {
        ""Attributes"": {
          ""SessionUrl"": ""session/10001"",
          ""PropertiesUrl"": ""session/10001/properties"",
          ""ControlMessageUrl"": ""session/10001/controlmessage"",
        },
        ""Channel"": ""VOICE"",
        ""ContactId"": ""ASDAcxcasDFSSDFs"",
       ""CustomerEndpoint"": {
          ""Address"": ""+17202950840"",
          ""Type"": ""TELEPHONE_NUMBER""
        },
        ""InitialContactId"": """",
        ""InitiationMethod"": ""INBOUND"",
        ""InstanceARN"": ""arn:aws:connect:us-east-1:396263001796:instance/1aad3ca7-ea11-4d98-bf4f-30a2644dd195"",
        ""PreviousContactId"": """",
        ""Queue"": null,
        ""SystemEndpoint"": {
          ""Address"": ""+17025346630"",
          ""Type"": ""TELEPHONE_NUMBER""
        }
      },
      ""Parameters"": {
        ""RequestName"": ""EndSession"",
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
        }

        [Fact]
        public async Task SuccessfullyEndsSession()
        {
            using (var testClient = new HttpClient(new SuccessfullyEndSession())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new EndSessionWorkflow(testClient);
                var response = await workflow.Process(_connectEvent, _context);

                Assert.True(response["LambdaResult"].Type == JTokenType.Boolean);
                Assert.True((bool)response["LambdaResult"]);
                Assert.Equal("disconnected", (string)response["SessionStatus"]);
            }
        }

        [Fact]
        public async Task FailsToEndSession()
        {
            using (var testClient = new HttpClient(new FailToEndSession())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new EndSessionWorkflow(testClient);
                var response = await workflow.Process(_connectEvent, _context);

                Assert.True(response["LambdaResult"].Type == JTokenType.Boolean);
                Assert.False((bool)response["LambdaResult"]);
                Assert.True(response["StatusCode"].Type == JTokenType.Integer);
                Assert.True(HttpStatusCode.NotFound == (HttpStatusCode)(int)response["StatusCode"]);
                Assert.Equal("disconnected", (string)response["SessionStatus"]);
                Assert.True(response["FailureReason"].Type == JTokenType.String);
            }
        }

        [Fact]
        public async Task FailsIfSessionUrlNotSpecified()
        {
            var badConnectEvent = JObject.Parse(@"{
    ""Details"": {
      ""ContactData"": {
        ""Attributes"": {},
        ""Channel"": ""VOICE"",
        ""ContactId"": ""ASDAcxcasDFSSDFs"",
       ""CustomerEndpoint"": {
          ""Address"": ""+17202950840"",
          ""Type"": ""TELEPHONE_NUMBER""
        },
        ""InitialContactId"": """",
        ""InitiationMethod"": ""INBOUND"",
        ""InstanceARN"": ""arn:aws:connect:us-east-1:396263001796:instance/1aad3ca7-ea11-4d98-bf4f-30a2644dd195"",
        ""PreviousContactId"": """",
        ""Queue"": null,
        ""SystemEndpoint"": {
          ""Address"": ""+17025346630"",
          ""Type"": ""TELEPHONE_NUMBER""
        }
      },
      ""Parameters"": {
        ""RequestName"": ""EndSession"",
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
            using (var testClient = new HttpClient(new SuccessfullyEndSession())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new EndSessionWorkflow(testClient);
                var response = await workflow.Process(badConnectEvent, _context);

                Assert.True(response["LambdaResult"].Type == JTokenType.Boolean);
                Assert.False((bool)response["LambdaResult"]);
                Assert.True(response["FailureReason"].Type == JTokenType.String);
            }
        }
    }
}