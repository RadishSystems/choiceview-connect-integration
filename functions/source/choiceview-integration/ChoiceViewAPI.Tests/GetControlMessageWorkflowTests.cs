using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.TestUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ChoiceViewAPI.Tests
{
    public class GetControlMessageWorkflowTests
    {
        private static readonly Dictionary<string, string> FormElements = new Dictionary<string, string>
        {
            {"menuNumber", "1"},
            {"menuName", "MockMenu"},
            {"buttonNumber", "0"},
            {"buttonName", "MockButton"}
        };

        public class SuccessfullyGetControlMessage : HttpClientHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(FormElements),
                        Encoding.UTF8, "application/json")
                };
            }
        }

        public class FailToGetControlMessage : HttpClientHandler
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

        public class GetControlMessageWithNoConent : HttpClientHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
        }
        private readonly JObject _connectEvent;
        private readonly TestLambdaContext _context;

        public GetControlMessageWorkflowTests(ITestOutputHelper output)
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
        ""RequestName"": ""GetControlMessage""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
        }

        [Fact]
        public async Task ReturnsControlMessage()
        {
            using (var testClient = new HttpClient(new SuccessfullyGetControlMessage())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new GetControlMessageWorkflow(testClient);
                var response = await workflow.Process(_connectEvent, _context);

                Assert.True(response.Count == 6);
                Assert.True(response["LambdaResult"].Type == JTokenType.Boolean);
                Assert.True((bool)response["LambdaResult"]);
                Assert.True(response["ControlMessageAvailable"].Type == JTokenType.Boolean);
                Assert.True((bool)response["ControlMessageAvailable"]);
                Assert.True(response["menuNumber"].Value<string>() == "1");
                Assert.True(response["menuName"].Value<string>() == "MockMenu");
                Assert.True(response["buttonNumber"].Value<string>() == "0");
                Assert.True(response["buttonName"].Value<string>() == "MockButton");
            }
        }

        [Fact]
        public async Task ReturnsNothingIfNoControlMessage()
        {
            using (var testClient = new HttpClient(new GetControlMessageWithNoConent())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new GetControlMessageWorkflow(testClient);
                var response = await workflow.Process(_connectEvent, _context);

                Assert.True(response.Count == 2);
                Assert.True(response["LambdaResult"].Type == JTokenType.Boolean);
                Assert.True((bool)response["LambdaResult"]);
                Assert.True(response["ControlMessageAvailable"].Type == JTokenType.Boolean);
                Assert.False((bool)response["ControlMessageAvailable"]);
            }
        }

        [Fact]
        public async Task FailsIfApiRequestFails()
        {
            using (var testClient = new HttpClient(new FailToGetControlMessage())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new GetControlMessageWorkflow(testClient);
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
        public async Task FailsIfNoControlMessageUrl()
        {
            var badConnectEvent = JObject.Parse(
                @"{
    ""Details"": {
      ""ContactData"": {
        ""Attributes"": {
          ""SessionUrl"": ""session/10001"",
          ""PropertiesUrl"": ""session/10001/properties"",
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
        ""RequestName"": ""GetControlMessage""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
            using (var testClient = new HttpClient(new SuccessfullyGetControlMessage())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new GetControlMessageWorkflow(testClient);
                var response = await workflow.Process(badConnectEvent, _context);

                Assert.True(response["LambdaResult"].Type == JTokenType.Boolean);
                Assert.False((bool)response["LambdaResult"]);
                Assert.True(response["FailureReason"].Type == JTokenType.String);
            }
        }
    }
}