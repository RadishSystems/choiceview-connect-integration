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
    public class SuccessfullyTransferSession : HttpClientHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return new HttpResponseMessage(request.RequestUri == new Uri(TransferSessionWorkflowTests.TransferUri) ?
                HttpStatusCode.OK : HttpStatusCode.BadRequest);
        }
    }

    public class FailToTransferSession : HttpClientHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var errorResponse = new JObject(new JProperty("Message", "Session not found"));

            return new HttpResponseMessage(request.RequestUri == new Uri(TransferSessionWorkflowTests.TransferUri) ?
                HttpStatusCode.NotFound : HttpStatusCode.BadRequest)
            {
                Content = new StringContent(errorResponse.ToString(), Encoding.UTF8, "application/json")
            };
        }
    }

    public class ServerFailToTransferSession : HttpClientHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var errorResponse = new JObject(new JProperty("Message", "Problem sending transfer request to ChoiceView Service!"));

            return new HttpResponseMessage(request.RequestUri == new Uri(TransferSessionWorkflowTests.TransferUri) ?
                HttpStatusCode.ServiceUnavailable : HttpStatusCode.BadRequest)
            {
                Content = new StringContent(errorResponse.ToString(), Encoding.UTF8, "application/json")
            };
        }
    }

    public class TransferSessionWorkflowTests
    {
        private readonly JObject _connectEvent;
        private readonly TestLambdaContext _context;

        private const string SessionUri = "https://cvnet2.radishsystems.com/ivr/api/session/10001";
        public const string TransferUri = SessionUri + "/transfer/radish1";

        public TransferSessionWorkflowTests(ITestOutputHelper output)
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
        ""RequestName"": ""TransferSession"",
        ""AccountId"": ""radish1""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
        }

        [Fact]
        public async Task SuccessfullyTransferSession()
        {
            using (var testClient = new HttpClient(new SuccessfullyTransferSession())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new TransferSessionWorkflow(testClient);
                var response = await workflow.Process(_connectEvent, _context);

                Assert.Equal(JTokenType.Boolean, response["LambdaResult"].Type);
                Assert.True((bool)response["LambdaResult"]);
            }
        }

        [Fact]
        public async Task FailsToTransferSession()
        {
            using (var testClient = new HttpClient(new FailToTransferSession())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new TransferSessionWorkflow(testClient);
                var response = await workflow.Process(_connectEvent, _context);

                Assert.Equal(JTokenType.Boolean, response["LambdaResult"].Type);
                Assert.False((bool)response["LambdaResult"]);
                Assert.Equal(JTokenType.Integer, response["StatusCode"].Type);
                Assert.Equal(HttpStatusCode.NotFound, (HttpStatusCode)(int)response["StatusCode"]);
                Assert.Equal("disconnected", (string)response["SessionStatus"]);
                Assert.Equal(JTokenType.String, response["FailureReason"].Type);
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
        ""RequestName"": ""TransferSession"",
        ""AccountId"": ""radish1""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
            using (var testClient = new HttpClient(new SuccessfullyTransferSession())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new TransferSessionWorkflow(testClient);
                var response = await workflow.Process(badConnectEvent, _context);

                Assert.Equal(JTokenType.Boolean, response["LambdaResult"].Type);
                Assert.False((bool)response["LambdaResult"]);
                Assert.False(response.TryGetValue("StatusCode", out var value));
                Assert.Equal(JTokenType.String, response["FailureReason"].Type);
            }
        }

        [Fact]
        public async Task FailsIfAccountIdNotSpecified()
        {
            var badConnectEvent = JObject.Parse(@"{
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
        ""RequestName"": ""TransferSession""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
            using (var testClient = new HttpClient(new SuccessfullyTransferSession())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new TransferSessionWorkflow(testClient);
                var response = await workflow.Process(badConnectEvent, _context);

                Assert.Equal(JTokenType.Boolean, response["LambdaResult"].Type);
                Assert.False((bool)response["LambdaResult"]);
                Assert.False(response.TryGetValue("StatusCode", out var value));
                Assert.Equal(JTokenType.String, response["FailureReason"].Type);
            }
        }

    }
}