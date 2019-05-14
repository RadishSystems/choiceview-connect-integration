using System;
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
    public class SuccessfullyGetSession : HttpClientHandler
    {
        private static string SessionUri = "https://cvnet2.radishsystems.com/ivr/api/session/10001";
        private static string PropertiesUri = "https://cvnet2.radishsystems.com/ivr/api/session/10001/properties";
        private static string ControlMessageUri = "https://cvnet2.radishsystems.com/ivr/api/session/10001/controlmessage";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var responseObject = new SessionResource
            {
                SessionId = 10001,
                Status = "connected",
                CallerId = "7202950840",
                CallId = "ASDAcxcasDFSSDFs"
            };

            responseObject.Links.Add(new Link
            {
                Rel = Link.SelfRel,
                Href = SessionUri
            });
            responseObject.Links.Add(new Link
            {
                Rel = Link.PayloadRel,
                Href = PropertiesUri
            });
            responseObject.Links.Add(new Link
            {
                Rel = Link.ControlMessageRel,
                Href = ControlMessageUri
            });

            responseObject.Properties["Property1"] = "PropertyValue1";
            responseObject.Properties["Property2"] = "PropertyValue2";

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonConvert.SerializeObject(responseObject),
                    Encoding.UTF8, "application/json")
            };

            return response;
        }
    }

    public class FailToGetSession : HttpClientHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var errorResponse = new JObject(new JProperty("Message", "Not found - Cannot find session."));

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(errorResponse.ToString(), Encoding.UTF8, "application/json")
            };
        }
    }

    public class GetSessionWorkflowTests
    {
        private readonly JObject connectEvent;
        private readonly TestLambdaContext context;

        public GetSessionWorkflowTests(ITestOutputHelper output)
        {
            context = new TestLambdaContext
            {
                Logger = new XUnitLambaLogger(output)
            };
            connectEvent = JObject.Parse(
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
        ""RequestName"": ""GetSession""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
        }

        [Fact]
        public async Task SuccessfullyGetSession()
        {
            using (var testClient = new HttpClient(new SuccessfullyGetSession())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new GetSessionWorkflow(testClient);
                var response = await workflow.Process(connectEvent, context);

                Assert.Equal(JTokenType.Boolean, response["LambdaResult"].Type);
                Assert.True((bool)response["LambdaResult"]);
                Assert.Equal("connected", (string)response["SessionStatus"]);
            }
        }

        [Fact]
        public async Task FailsToGetSession()
        {
            using (var testClient = new HttpClient(new FailToGetSession())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new GetSessionWorkflow(testClient);
                var response = await workflow.Process(connectEvent, context);

                Assert.Equal(JTokenType.Boolean, response["LambdaResult"].Type);
                Assert.True((bool)response["LambdaResult"]);
                Assert.Equal("disconnected", (string)response["SessionStatus"]);
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
        ""RequestName"": ""GetSession"",
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
            using (var testClient = new HttpClient(new SuccessfullyGetSession())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new GetSessionWorkflow(testClient);
                var response = await workflow.Process(badConnectEvent, context);

                Assert.Equal(JTokenType.Boolean, response["LambdaResult"].Type);
                Assert.False((bool)response["LambdaResult"]);
                Assert.Equal(JTokenType.String, response["FailureReason"].Type);
            }
        }

    }
}