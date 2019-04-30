using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.Lambda.TestUtilities;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ChoiceViewAPI.Tests
{
    public class QuerySessionWorkflowTests
    {
        private readonly JObject connectEvent;
        private readonly TestLambdaContext context;

        public QuerySessionWorkflowTests(ITestOutputHelper output)
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
          ""QueryUrl"": ""sessions?callid=ASDAcxcasDFSSDFs"",
          ""ConnectStartTime"": ""2018-01-01T07:22Z""
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
        ""RequestName"": ""QuerySession""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
        }

        [Fact]
        public async Task SuccessfullyQuerySession()
        {
            using (var testClient = new HttpClient(new SuccessfullyGetSession())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new QuerySessionWorkflow(testClient);
                var response = await workflow.Process(connectEvent, context);

                Assert.Equal(JTokenType.Boolean, response["LambdaResult"].Type);
                Assert.True((bool)response["LambdaResult"]);
                Assert.Equal("connected", (string)response["SessionStatus"]);
                Assert.Equal(JTokenType.String, response["SessionUrl"].Type);
                Assert.Equal(JTokenType.String, response["PropertiesUrl"].Type);
                Assert.Equal(JTokenType.String, response["ControlMessageUrl"].Type);
                Assert.Equal(JTokenType.String, response["Property1"].Type);
                Assert.Equal(JTokenType.String, response["Property2"].Type);
            }
        }

        [Fact]
        public async Task FailsToQuerySession()
        {
            using (var testClient = new HttpClient(new FailToEndSession())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new QuerySessionWorkflow(testClient);
                var response = await workflow.Process(connectEvent, context);

                Assert.Equal(JTokenType.Boolean, response["LambdaResult"].Type);
                Assert.True((bool)response["LambdaResult"]);
                Assert.Equal(JTokenType.Boolean, response["SessionRetrieved"].Type);
                Assert.False((bool)response["SessionRetrieved"]);
                Assert.Equal(JTokenType.Boolean, response["SessionTimeout"].Type);
                Assert.True((bool)response["SessionTimeout"]);
            }
        }
    }
}