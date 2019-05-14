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
    public class SuccessfullyAddProperty : HttpClientHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    public class FailToAddProperty : HttpClientHandler
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

    public class AddPropertyWorkflowTests
    {
        public static string PropertiesUri = "session/10001/properties";

        private readonly JObject connectEvent;
        private readonly TestLambdaContext context;

        public AddPropertyWorkflowTests(ITestOutputHelper output)
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
        ""RequestName"": ""AddProperty"",
        ""PropertyName"": ""Property3"",
        ""PropertyValue"": ""PropertyValue3""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
        }

        [Fact]
        public async Task SuccessfullyAddProperty()
        {
            using (var testClient = new HttpClient(new SuccessfullyAddProperty())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new AddPropertyWorkflow(testClient);
                var response = await workflow.Process(connectEvent, context);

                Assert.True(response["LambdaResult"].Type == JTokenType.Boolean);
                Assert.True((bool)response["LambdaResult"]);
            }
        }

        [Fact]
        public async Task FailsToAddProperty()
        {
            using (var testClient = new HttpClient(new FailToAddProperty())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new AddPropertyWorkflow(testClient);
                var response = await workflow.Process(connectEvent, context);

                Assert.True(response["LambdaResult"].Type == JTokenType.Boolean);
                Assert.False((bool)response["LambdaResult"]);
                Assert.True(response["StatusCode"].Type == JTokenType.Integer);
                Assert.True(HttpStatusCode.NotFound == (HttpStatusCode)(int)response["StatusCode"]);
                Assert.Equal("disconnected", (string)response["SessionStatus"]);
                Assert.True(response["FailureReason"].Type == JTokenType.String);
            }
        }

        [Fact]
        public async Task FailsIfPropertiesUrlNotSpecified()
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
        ""RequestName"": ""AddProperty"",
        ""PropertyName"": ""Property3"",
        ""PropertyValue"": ""PropertyValue3""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
            using (var testClient = new HttpClient(new SuccessfullyAddProperty())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new AddPropertyWorkflow(testClient);
                var response = await workflow.Process(badConnectEvent, context);

                Assert.True(response["LambdaResult"].Type == JTokenType.Boolean);
                Assert.False((bool)response["LambdaResult"]);
                Assert.True(response["FailureReason"].Type == JTokenType.String);
            }
        }

        [Fact]
        public async Task FailsIfPropertyNameNotSpecified()
        {
            var badConnectEvent = JObject.Parse(@"{
    ""Details"": {
      ""ContactData"": {
        ""Attributes"": {
          ""SessionUrl"": ""session/10001"",
          ""PropertiesUrl"": ""session/10001/properties"",
          ""ControlMessageUrl"": ""session/10001/controlmessage"",
          ""SessionStatus"": ""connected""
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
        ""RequestName"": ""AddProperty""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
            using (var testClient = new HttpClient(new SuccessfullySendUrl())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new AddPropertyWorkflow(testClient);
                var response = await workflow.Process(badConnectEvent, context);

                Assert.True(response["LambdaResult"].Type == JTokenType.Boolean);
                Assert.False((bool)response["LambdaResult"]);
                Assert.True(response["FailureReason"].Type == JTokenType.String);
            }
        }
    }
}