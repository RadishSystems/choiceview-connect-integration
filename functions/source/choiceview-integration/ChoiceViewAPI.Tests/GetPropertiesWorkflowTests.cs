using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.TestUtilities;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Twilio;
using Xunit;
using Xunit.Abstractions;
using HttpClient = System.Net.Http.HttpClient;

namespace ChoiceViewAPI.Tests
{
    public class SuccessfulGetProperties : HttpClientHandler
    {
        private const string SessionUri = "https://cvnet2.radishsystems.com/ivr/api/session/10001";
        private const string PropertiesUri = "https://cvnet2.radishsystems.com/ivr/api/session/10001/properties";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var responseObject = new PropertiesResource
            {
                SessionId = 10001,
            };

            responseObject.Links.Add(new Link
            {
                Rel = Link.SessionRel,
                Href = SessionUri
            });
            responseObject.Links.Add(new Link
            {
                Rel = Link.SelfRel,
                Href = PropertiesUri
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

    public class NotFoundGetProperties : HttpClientHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var errorResponse = new JObject(new JProperty("message", "Request timeout - no response from ChoiceView client."));

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(errorResponse.ToString(), Encoding.UTF8, "application/json")
            };
        }
    }

    public class GetPropertiesWorkflowTests
    {
        private readonly JObject connectEvent;
        private readonly TestLambdaContext context;

        public GetPropertiesWorkflowTests(ITestOutputHelper output)
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
          ""SessionStatus"": ""connected""
        },
        ""Channel"": ""VOICE"",
        ""ContactId"": ""ASDAcxcasDFSSDFs"",
       ""CustomerEndpoint"": {
          ""Address"": ""+17202950840"",
          ""Type"": ""TELEPHONE_NUMBER""
        },
        ""InitialContactId"": ""ASDAcxcasDFSSDFs"",
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
        ""RequestName"": ""GetProperties""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
        }

        [Fact]
        public async Task ReturnsPropertiesResource()
        {
            using (var testClient = new HttpClient(new SuccessfulGetProperties())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new GetPropertiesWorkflow(testClient);
                var response = await workflow.Process(connectEvent, context);

                Assert.Equal(JTokenType.Boolean, response["LambdaResult"].Type);
                Assert.True((bool) response["LambdaResult"]);
                Assert.Equal(JTokenType.String, response["Property1"].Type);
                Assert.Equal(JTokenType.String, response["Property2"].Type);
            }
        }

        [Fact]
        public async Task NoSessionFound()
        {
            using (var testClient = new HttpClient(new NotFoundGetProperties())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new GetPropertiesWorkflow(testClient);
                var response = await workflow.Process(connectEvent, context);

                Assert.True(response["LambdaResult"].Type == JTokenType.Boolean);
                Assert.False((bool) response["LambdaResult"]);
                Assert.True(response["StatusCode"].Type == JTokenType.Integer);
                Assert.True(HttpStatusCode.NotFound == (HttpStatusCode) (int) response["StatusCode"]);
            }
        }
    }
}