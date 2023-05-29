using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.TestUtilities;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.SecurityToken;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Twilio;
using Xunit;
using Xunit.Abstractions;
using HttpClient = System.Net.Http.HttpClient;

namespace ChoiceViewAPI.Tests
{
    public class SuccessfulCreateSession : HttpClientHandler
    {
        private const string SessionUri = "https://cvnet2.radishsystems.com/ivr/api/session/10001";
        private const string PropertiesUri = "https://cvnet2.radishsystems.com/ivr/api/session/10001/properties";
        private const string ControlMessageUri = "https://cvnet2.radishsystems.com/ivr/api/session/10001/controlmessage";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            dynamic requestParams = JObject.Parse(await request.Content.ReadAsStringAsync());
            var responseObject = new SessionResource
            {
                SessionId = 10001,
                Status = "connected",
                CallerId = requestParams.callerId,
                CallId = requestParams.callId
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

            var response = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(JsonConvert.SerializeObject(responseObject),
                    Encoding.UTF8, "application/json")
            };
            response.Headers.Location = new Uri(SessionUri);

            return response;
        }
    }

    public class TimeoutCreateSession : HttpClientHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var errorResponse = new JObject(new JProperty("message", "Request timeout - no response from ChoiceView client."));

            return new HttpResponseMessage(HttpStatusCode.RequestTimeout)
            {
                Content = new StringContent(errorResponse.ToString(), Encoding.UTF8, "application/json")
            };
        }
    }

    public class SuccessfulCreateSessionImmediateReturn : HttpClientHandler
    {
        private const string QueryUri = "https://cvnet2.radishsystems.com/ivr/api/sessions?callid=";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            dynamic requestParams = JObject.Parse(await request.Content.ReadAsStringAsync());
            dynamic responseObject = new JObject();
            responseObject.Message =
                "Waitng for the client to connect to the ChoiceView server. Use the query url to get the session resource.";
            responseObject.QueryUrl =  QueryUri + requestParams.callId;

            var response = new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent(JsonConvert.SerializeObject(responseObject),
                    Encoding.UTF8, "application/json")
            };

            return response;
        }
    }

    public class CreateSessionWorkflowTests
    {
        private readonly JObject connectEvent;
        private readonly TestLambdaContext context;
        private const string QueryUrl = "sessions?callid=ASDAcxcasDFSSDFs";

        public CreateSessionWorkflowTests(ITestOutputHelper output)
        {
            context = new TestLambdaContext
            {
                Logger = new XUnitLambaLogger(output)
            };
            connectEvent = JObject.Parse(
                @"{
    ""Details"": {
      ""ContactData"": {
        ""Attributes"": {},
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
        ""RequestName"": ""CreateSession""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
        }

        [Fact]
        public async Task ReturnsQueryUrlWhenCallerStartsSession()
        {
            using (var testClient = new HttpClient(new SuccessfulCreateSessionImmediateReturn())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new CreateSessionWorkflow(testClient);
                var response = await workflow.Process(connectEvent, context);

                Assert.Equal(JTokenType.Boolean, response["LambdaResult"].Type);
                Assert.True((bool) response["LambdaResult"]);
                Assert.Equal(QueryUrl, (string) response["QueryUrl"]);
                Assert.False(response.TryGetValue("SessionStatus", out var value));
                Assert.False(response.TryGetValue("SessionUrl", out value));
                Assert.False(response.TryGetValue("PropertiesUrl", out value));
                Assert.False(response.TryGetValue("ControlMessageUrl", out value));
                Assert.True(response.TryGetValue("ConnectStartTime", out value));
                Assert.Equal(JTokenType.Date, value.Type);
            }
        }

        [Fact]
        public async Task TimesOutWhenCallerDoesNotStartsSession()
        {
            using (var testClient = new HttpClient(new TimeoutCreateSession())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                var workflow = new CreateSessionWorkflow(testClient);
                var response = await workflow.Process(connectEvent, context);

                Assert.True(response["LambdaResult"].Type == JTokenType.Boolean);
                Assert.False((bool) response["LambdaResult"]);
                Assert.True(response["StatusCode"].Type == JTokenType.Integer);
                Assert.True(HttpStatusCode.RequestTimeout == (HttpStatusCode) (int) response["StatusCode"]);
                Assert.True(response["Timeout"].Type == JTokenType.Boolean);
                Assert.True((bool) response["Timeout"]);
                Assert.True(response["FailureReason"].Type == JTokenType.String);
            }
        }
    }

    public class CreateSessionWithTwilioSmsWorkflowTests
    {
        private readonly JObject connectEvent;
        private readonly JObject connectEventWithSkipNumberCheckSet;
        private readonly JObject connectEventWithoutSmsMessage;
        private readonly JObject connectEventWithoutClientUrl;
        private readonly TestLambdaContext context;
        private const string SmsNumber = "+15005550006";
        private const string QueryUrl = "sessions?callid=ASDAcxcasDFSSDFs";

        public CreateSessionWithTwilioSmsWorkflowTests()
        {
            Environment.SetEnvironmentVariable("TWILIO_ACCOUNTSID", "ACxxxxxxxxxxxxxx");
            Environment.SetEnvironmentVariable("TWILIO_AUTHTOKEN", "TestAuthTokenValue");
            context = new TestLambdaContext();
            connectEvent = JObject.Parse(
                @"{
    ""Details"": {
      ""ContactData"": {
        ""Attributes"": {},
        ""Channel"": ""VOICE"",
        ""ContactId"": ""ASDAcxcasDFSSDFs"",
        ""CustomerEndpoint"": {
          ""Address"": ""+17202950840"",
          ""Type"": ""TELEPHONE_NUMBER""
        },
        ""InitialContactId"": ""ASDAcxcasDFSSDFs"",
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
        ""RequestName"": ""CreateSessionWithSms"",
        ""SmsMessage"": ""Tap this link to start ChoiceView: http://choiceview.com/start.html?account=radish1&phone=""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
            connectEventWithSkipNumberCheckSet = JObject.Parse(
                @"{
    ""Details"": {
      ""ContactData"": {
        ""Attributes"": {},
        ""Channel"": ""VOICE"",
        ""ContactId"": ""ASDAcxcasDFSSDFs"",
        ""CustomerEndpoint"": {
          ""Address"": ""+17202950840"",
          ""Type"": ""TELEPHONE_NUMBER""
        },
        ""InitialContactId"": ""ASDAcxcasDFSSDFs"",
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
        ""RequestName"": ""CreateSessionWithSms"",
        ""SmsMessage"": ""Tap this link to start ChoiceView: http://choiceview.com/start.html?account=radish1&phone="",
        ""SkipNumberCheck"": ""true""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
            connectEventWithoutSmsMessage = JObject.Parse(
                @"{
    ""Details"": {
      ""ContactData"": {
        ""Attributes"": {},
        ""Channel"": ""VOICE"",
        ""ContactId"": ""ASDAcxcasDFSSDFs"",
        ""CustomerEndpoint"": {
          ""Address"": ""+17202950840"",
          ""Type"": ""TELEPHONE_NUMBER""
        },
        ""InitialContactId"": ""ASDAcxcasDFSSDFs"",
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
        ""RequestName"": ""CreateSessionWithSms""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
            connectEventWithoutClientUrl = JObject.Parse(
                @"{
    ""Details"": {
      ""ContactData"": {
        ""Attributes"": {},
        ""Channel"": ""VOICE"",
        ""ContactId"": ""ASDAcxcasDFSSDFs"",
        ""CustomerEndpoint"": {
          ""Address"": ""+17202950840"",
          ""Type"": ""TELEPHONE_NUMBER""
        },
        ""InitialContactId"": ""ASDAcxcasDFSSDFs"",
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
        ""RequestName"": ""CreateSessionWithSms"",
        ""SmsMessage"": ""Welcome to the unit test!""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
        }

        [Fact]
        public async Task SendsSmsAndReturnsQueryUrlWhenCallerStartsSessionWithSms()
        {
            var smsBodyNumber = connectEvent.SelectToken("Details.ContactData.CustomerEndpoint.Address")
                .Value<string>()
                .Substring(1);
            var lookupsMock = new Mock<ITwilioLookupsApi>(MockBehavior.Strict);
            lookupsMock.Setup(api => api.NumberInfo(It.IsAny<string>(), It.IsAny<string>(), "carrier"))
                .ReturnsAsync((string phoneNumber, string countryCode, string type) =>
                    JObject.Parse($"{{\"url\": \"https://lookups.twilio.com/v1/PhoneNumbers/{phoneNumber}?Type=carrier\",\"carrier\": {{ \"type\": \"mobile\" }},\"phone_number\": \"{phoneNumber}\",\"country_code\": \"{countryCode}\"}}"));

            var messagingMock = new Mock<ITwilioMessagingApi>(MockBehavior.Strict);
            messagingMock.Setup(api => api.SendSMS(It.IsAny<string>(), It.Is<Dictionary<string, string>>(
                    args => args["From"].Equals(SmsNumber) && args["Body"].Contains($"={IVRWorkflow.SwitchCallerId(smsBodyNumber)}"))))
                .ReturnsAsync((string accountSid, Dictionary<string, string> args) =>
                    JObject.Parse($"{{\"account_sid\": \"{accountSid}\",\"api_version\": \"2010-04-01\",\"body\": \"{args["Body"]}\",\"from\": \"{args["From"]}\",\"status\": \"sent\",\"to\": \"{args["To"]}\"}}"));

            var twilioApi = new TwilioApi(lookupsMock.Object, messagingMock.Object, "ACxxxxxxxxxxxxxx", SmsNumber);

            var smsWorkflow = new TwilioSmsWorkflow(twilioApi);

            using (var testClient = new HttpClient(new SuccessfulCreateSessionImmediateReturn())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                try
                {
                    Environment.SetEnvironmentVariable("TWILIO_PHONENUMBER", SmsNumber);
                    var workflow = new CreateSessionWorkflow(testClient, smsWorkflow);
                    var response = await workflow.Process(connectEvent, context);

                    Assert.Equal(JTokenType.Boolean, response["LambdaResult"].Type);
                    Assert.True((bool)response["LambdaResult"]);
                    Assert.Equal(QueryUrl, response["QueryUrl"].Value<string>());
                    Assert.False(response.TryGetValue("SessionStatus", out var value));
                    Assert.False(response.TryGetValue("SessionUrl", out value));
                    Assert.False(response.TryGetValue("PropertiesUrl", out value));
                    Assert.False(response.TryGetValue("ControlMessageUrl", out value));
                    Assert.True(response.TryGetValue("ConnectStartTime", out value));
                    Assert.Equal(JTokenType.Date, value.Type);
                }
                finally
                {
                    Environment.SetEnvironmentVariable("TWILIO_PHONENUMBER", null);
                }
            }

            lookupsMock.VerifyAll();
            messagingMock.VerifyAll();
        }

        [Fact]
        public async Task SendsSmsAndReturnsQueryUrlWhenCallerStartsSessionWithSmsWithoutSmsMessageParameter()
        {
            var lookupsMock = new Mock<ITwilioLookupsApi>(MockBehavior.Strict);
            lookupsMock.Setup(api => api.NumberInfo(It.IsAny<string>(), It.IsAny<string>(), "carrier"))
                .ReturnsAsync((string phoneNumber, string countryCode, string type) =>
                    JObject.Parse($"{{\"url\": \"https://lookups.twilio.com/v1/PhoneNumbers/{phoneNumber}?Type=carrier\",\"carrier\": {{ \"type\": \"mobile\" }},\"phone_number\": \"{phoneNumber}\",\"country_code\": \"{countryCode}\"}}"));

            var messagingMock = new Mock<ITwilioMessagingApi>(MockBehavior.Strict);
            messagingMock.Setup(api => api.SendSMS(It.IsAny<string>(), It.Is<Dictionary<string, string>>(
                    args => args["From"].Equals(SmsNumber))))
                .ReturnsAsync((string accountSid, Dictionary<string, string> args) =>
                    JObject.Parse($"{{\"account_sid\": \"{accountSid}\",\"api_version\": \"2010-04-01\",\"body\": \"{args["Body"]}\",\"from\": \"{args["From"]}\",\"status\": \"sent\",\"to\": \"{args["To"]}\"}}"));

            var twilioApi = new TwilioApi(lookupsMock.Object, messagingMock.Object, "ACxxxxxxxxxxxxxx", SmsNumber);

            var smsWorkflow = new TwilioSmsWorkflow(twilioApi);

            using (var testClient = new HttpClient(new SuccessfulCreateSessionImmediateReturn())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                try
                {
                    Environment.SetEnvironmentVariable("TWILIO_PHONENUMBER", SmsNumber);
                    var workflow = new CreateSessionWorkflow(testClient, smsWorkflow);
                    var response = await workflow.Process(connectEventWithoutSmsMessage, context);

                    Assert.Equal(JTokenType.Boolean, response["LambdaResult"].Type);
                    Assert.True((bool)response["LambdaResult"]);
                    Assert.Equal(QueryUrl, response["QueryUrl"].Value<string>());
                    Assert.False(response.TryGetValue("SessionStatus", out var value));
                    Assert.False(response.TryGetValue("SessionUrl", out value));
                    Assert.False(response.TryGetValue("PropertiesUrl", out value));
                    Assert.False(response.TryGetValue("ControlMessageUrl", out value));
                    Assert.True(response.TryGetValue("ConnectStartTime", out value));
                    Assert.Equal(JTokenType.Date, value.Type);
                }
                finally
                {
                    Environment.SetEnvironmentVariable("TWILIO_PHONENUMBER", null);
                }
            }
        }

        [Fact]
        public async Task SendsSmsAndReturnsQueryUrlWhenCallerStartsSessionWithoutClientUrl()
        {
            var smsBodyNumber = connectEventWithoutClientUrl.SelectToken("Details.ContactData.CustomerEndpoint.Address")
                .Value<string>()
                .Substring(1);
            var lookupsMock = new Mock<ITwilioLookupsApi>(MockBehavior.Strict);
            lookupsMock.Setup(api => api.NumberInfo(It.IsAny<string>(), It.IsAny<string>(), "carrier"))
                .ReturnsAsync((string phoneNumber, string countryCode, string type) =>
                    JObject.Parse($"{{\"url\": \"https://lookups.twilio.com/v1/PhoneNumbers/{phoneNumber}?Type=carrier\",\"carrier\": {{ \"type\": \"mobile\" }},\"phone_number\": \"{phoneNumber}\",\"country_code\": \"{countryCode}\"}}"));

            var messagingMock = new Mock<ITwilioMessagingApi>(MockBehavior.Strict);
            messagingMock.Setup(api => api.SendSMS(It.IsAny<string>(), It.Is<Dictionary<string, string>>(
                    args => args["From"].Equals(SmsNumber) && args["Body"].Contains($"={IVRWorkflow.SwitchCallerId(smsBodyNumber)}"))))
                .ReturnsAsync((string accountSid, Dictionary<string, string> args) =>
                    JObject.Parse($"{{\"account_sid\": \"{accountSid}\",\"api_version\": \"2010-04-01\",\"body\": \"{args["Body"]}\",\"from\": \"{args["From"]}\",\"status\": \"sent\",\"to\": \"{args["To"]}\"}}"));

            var twilioApi = new TwilioApi(lookupsMock.Object, messagingMock.Object, "ACxxxxxxxxxxxxxx", SmsNumber);

            var smsWorkflow = new TwilioSmsWorkflow(twilioApi);

            using (var testClient = new HttpClient(new SuccessfulCreateSessionImmediateReturn())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                try
                {
                    Environment.SetEnvironmentVariable("TWILIO_PHONENUMBER", SmsNumber);
                    var workflow = new CreateSessionWorkflow(testClient, smsWorkflow);
                    var response = await workflow.Process(connectEventWithoutClientUrl, context);

                    Assert.Equal(JTokenType.Boolean, response["LambdaResult"].Type);
                    Assert.True((bool)response["LambdaResult"]);
                    Assert.Equal(QueryUrl, response["QueryUrl"].Value<string>());
                    Assert.False(response.TryGetValue("SessionStatus", out var value));
                    Assert.False(response.TryGetValue("SessionUrl", out value));
                    Assert.False(response.TryGetValue("PropertiesUrl", out value));
                    Assert.False(response.TryGetValue("ControlMessageUrl", out value));
                    Assert.True(response.TryGetValue("ConnectStartTime", out value));
                    Assert.Equal(JTokenType.Date, value.Type);
                }
                finally
                {
                    Environment.SetEnvironmentVariable("TWILIO_PHONENUMBER", null);
                }
            }

            lookupsMock.VerifyAll();
            messagingMock.VerifyAll();
        }

        [Fact]
        public async Task SendsSmsAndReturnsQueryUrlWhenCallerStartsSessionWithSmsAndSkipNumberCheckIsTrue()
        {
            var smsBodyNumber = connectEvent.SelectToken("Details.ContactData.CustomerEndpoint.Address")
                .Value<string>()
                .Substring(1);
            var lookupsMock = new Mock<ITwilioLookupsApi>(MockBehavior.Strict);
            lookupsMock.Setup(api => api.NumberInfo(It.IsAny<string>(), It.IsAny<string>(), "carrier"))
                .ReturnsAsync((string phoneNumber, string countryCode, string type) =>
                    JObject.Parse($"{{\"url\": \"https://lookups.twilio.com/v1/PhoneNumbers/{phoneNumber}?Type=carrier\",\"carrier\": {{ \"type\": \"landline\" }},\"phone_number\": \"{phoneNumber}\",\"country_code\": \"{countryCode}\"}}"));

            var messagingMock = new Mock<ITwilioMessagingApi>(MockBehavior.Strict);
            messagingMock.Setup(api => api.SendSMS(It.IsAny<string>(), It.Is<Dictionary<string, string>>(
                    args => args["From"].Equals(SmsNumber) && args["Body"].Contains($"={IVRWorkflow.SwitchCallerId(smsBodyNumber)}"))))
                .ReturnsAsync((string accountSid, Dictionary<string, string> args) =>
                    JObject.Parse($"{{\"account_sid\": \"{accountSid}\",\"api_version\": \"2010-04-01\",\"body\": \"{args["Body"]}\",\"from\": \"{args["From"]}\",\"status\": \"sent\",\"to\": \"{args["To"]}\"}}"));

            var twilioApi = new TwilioApi(lookupsMock.Object, messagingMock.Object, "ACxxxxxxxxxxxxxx", SmsNumber);

            var smsWorkflow = new TwilioSmsWorkflow(twilioApi);

            using (var testClient = new HttpClient(new SuccessfulCreateSessionImmediateReturn())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            })
            {
                try
                {
                    Environment.SetEnvironmentVariable("TWILIO_PHONENUMBER", SmsNumber);
                    var workflow = new CreateSessionWorkflow(testClient, smsWorkflow);
                    var response = await workflow.Process(connectEventWithSkipNumberCheckSet, context);

                    Assert.Equal(JTokenType.Boolean, response["LambdaResult"].Type);
                    Assert.True((bool)response["LambdaResult"]);
                    Assert.Equal(QueryUrl, response["QueryUrl"].Value<string>());
                    Assert.False(response.TryGetValue("SessionStatus", out var value));
                    Assert.False(response.TryGetValue("SessionUrl", out value));
                    Assert.False(response.TryGetValue("PropertiesUrl", out value));
                    Assert.False(response.TryGetValue("ControlMessageUrl", out value));
                    Assert.True(response.TryGetValue("ConnectStartTime", out value));
                    Assert.Equal(JTokenType.Date, value.Type);
                }
                finally
                {
                    Environment.SetEnvironmentVariable("TWILIO_PHONENUMBER", null);
                }
            }

            lookupsMock.Verify(api => api.NumberInfo(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>()), Times.Never);
            messagingMock.VerifyAll();
        }
    }

    public class CreateSessionWithAmazonSmsWorkflowTests
    {
        private readonly AWSCredentials _credentials;
        private readonly AmazonSecurityTokenServiceClient _tokenService;
        private readonly JObject connectEvent;
        private readonly JObject connectEventWithSkipNumberCheckSet;
        private readonly JObject connectEventWithoutSmsMessage;
        private readonly JObject connectEventWithoutClientUrl;
        private readonly TestLambdaContext context;
        private const string SmsNumber = "+15005550006";
        private const string QueryUrl = "sessions?callid=ASDAcxcasDFSSDFs";

        public CreateSessionWithAmazonSmsWorkflowTests()
        {
            var profileStore = new CredentialProfileStoreChain();
            if (!profileStore.TryGetAWSCredentials("connect-development", out _credentials))
            {
                _credentials = new AnonymousAWSCredentials();
            }

            _tokenService = new AmazonSecurityTokenServiceClient(_credentials);

            context = new TestLambdaContext();
            connectEvent = JObject.Parse(
                @"{
    ""Details"": {
      ""ContactData"": {
        ""Attributes"": {},
        ""Channel"": ""VOICE"",
        ""ContactId"": ""ASDAcxcasDFSSDFs"",
        ""CustomerEndpoint"": {
          ""Address"": ""+17202950840"",
          ""Type"": ""TELEPHONE_NUMBER""
        },
        ""InitialContactId"": ""ASDAcxcasDFSSDFs"",
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
        ""RequestName"": ""CreateSessionWithSms"",
        ""SmsMessage"": ""Tap this link to start ChoiceView: http://choiceview.com/start.html?account=radish1&phone=""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
            connectEventWithSkipNumberCheckSet = JObject.Parse(
                @"{
    ""Details"": {
      ""ContactData"": {
        ""Attributes"": {},
        ""Channel"": ""VOICE"",
        ""ContactId"": ""ASDAcxcasDFSSDFs"",
        ""CustomerEndpoint"": {
          ""Address"": ""+17202950840"",
          ""Type"": ""TELEPHONE_NUMBER""
        },
        ""InitialContactId"": ""ASDAcxcasDFSSDFs"",
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
        ""RequestName"": ""CreateSessionWithSms"",
        ""SmsMessage"": ""Tap this link to start ChoiceView: http://choiceview.com/start.html?account=radish1&phone="",
        ""SkipNumberCheck"": ""true""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
            connectEventWithoutSmsMessage = JObject.Parse(
                @"{
    ""Details"": {
      ""ContactData"": {
        ""Attributes"": {},
        ""Channel"": ""VOICE"",
        ""ContactId"": ""ASDAcxcasDFSSDFs"",
        ""CustomerEndpoint"": {
          ""Address"": ""+17202950840"",
          ""Type"": ""TELEPHONE_NUMBER""
        },
        ""InitialContactId"": ""ASDAcxcasDFSSDFs"",
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
        ""RequestName"": ""CreateSessionWithSms"",
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
            connectEventWithoutClientUrl = JObject.Parse(
                @"{
    ""Details"": {
      ""ContactData"": {
        ""Attributes"": {},
        ""Channel"": ""VOICE"",
        ""ContactId"": ""ASDAcxcasDFSSDFs"",
        ""CustomerEndpoint"": {
          ""Address"": ""+17202950840"",
          ""Type"": ""TELEPHONE_NUMBER""
        },
        ""InitialContactId"": ""ASDAcxcasDFSSDFs"",
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
        ""RequestName"": ""CreateSessionWithSms"",
        ""SmsMessage"": ""Welcome to the unit test!""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
        }

        [Fact]
        public async Task SendsSmsAndReturnsQueryUrlWhenCallerStartsSessionWithSms()
        {
            var smsBodyNumber = connectEvent.SelectToken("Details.ContactData.CustomerEndpoint.Address")
                .Value<string>()
                .Substring(1);

            var smsWorkflow = new AwsSmsWorkflow(_credentials);

            using var testClient = new HttpClient(new SuccessfulCreateSessionImmediateReturn())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            };

            try
            {
                Environment.SetEnvironmentVariable("UseAwsSms", "true");
                var workflow = new CreateSessionWorkflow(testClient, awsSmsWorkflow: smsWorkflow);
                var response = await workflow.Process(connectEvent, context);

                Assert.Equal(JTokenType.Boolean, response["LambdaResult"].Type);
                Assert.True((bool)response["LambdaResult"]);
                Assert.Equal(QueryUrl, response["QueryUrl"].Value<string>());
                Assert.False(response.TryGetValue("SessionStatus", out var value));
                Assert.False(response.TryGetValue("SessionUrl", out value));
                Assert.False(response.TryGetValue("PropertiesUrl", out value));
                Assert.False(response.TryGetValue("ControlMessageUrl", out value));
                Assert.True(response.TryGetValue("ConnectStartTime", out value));
                Assert.Equal(JTokenType.Date, value.Type);
            }
            finally
            {
                Environment.SetEnvironmentVariable("UseAwsSms", "false");
            }
        }

        [Fact]
        public async Task SendsSmsAndReturnsQueryUrlWhenCallerStartsSessionWithSmsWithoutSmsMessageParameter()
        {
            var smsWorkflow = new AwsSmsWorkflow(_credentials);

            using var testClient = new HttpClient(new SuccessfulCreateSessionImmediateReturn())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            };
            try
            {
                Environment.SetEnvironmentVariable("UseAwsSms", "true");
                var workflow = new CreateSessionWorkflow(testClient, awsSmsWorkflow: smsWorkflow);
                var response = await workflow.Process(connectEventWithoutSmsMessage, context);

                Assert.Equal(JTokenType.Boolean, response["LambdaResult"].Type);
                Assert.True((bool)response["LambdaResult"]);
                Assert.Equal(QueryUrl, response["QueryUrl"].Value<string>());
                Assert.False(response.TryGetValue("SessionStatus", out var value));
                Assert.False(response.TryGetValue("SessionUrl", out value));
                Assert.False(response.TryGetValue("PropertiesUrl", out value));
                Assert.False(response.TryGetValue("ControlMessageUrl", out value));
                Assert.True(response.TryGetValue("ConnectStartTime", out value));
                Assert.Equal(JTokenType.Date, value.Type);
            }
            finally
            {
                Environment.SetEnvironmentVariable("UseAwsSms", "false");
            }
        }

        [Fact]
        public async Task SendsSmsAndReturnsQueryUrlWhenCallerStartsSessionWithoutClientUrl()
        {
            var smsWorkflow = new AwsSmsWorkflow(_credentials);

            using var testClient = new HttpClient(new SuccessfulCreateSessionImmediateReturn())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            };
            {
                try
                {
                    Environment.SetEnvironmentVariable("UseAwsSms", "true");
                    var workflow = new CreateSessionWorkflow(testClient, awsSmsWorkflow: smsWorkflow);
                    var response = await workflow.Process(connectEventWithoutClientUrl, context);

                    Assert.Equal(JTokenType.Boolean, response["LambdaResult"].Type);
                    Assert.True((bool)response["LambdaResult"]);
                    Assert.Equal(QueryUrl, response["QueryUrl"].Value<string>());
                    Assert.False(response.TryGetValue("SessionStatus", out var value));
                    Assert.False(response.TryGetValue("SessionUrl", out value));
                    Assert.False(response.TryGetValue("PropertiesUrl", out value));
                    Assert.False(response.TryGetValue("ControlMessageUrl", out value));
                    Assert.True(response.TryGetValue("ConnectStartTime", out value));
                    Assert.Equal(JTokenType.Date, value.Type);
                }
                finally
                {
                    Environment.SetEnvironmentVariable("UseAwsSms", "false");
                }
            }
        }

        [Fact]
        public async Task SendsSmsAndReturnsQueryUrlWhenCallerStartsSessionWithSmsAndSkipNumberCheckIsTrue()
        {
            var smsWorkflow = new AwsSmsWorkflow(_credentials);

            using var testClient = new HttpClient(new SuccessfulCreateSessionImmediateReturn())
            {
                BaseAddress = new Uri("https://cvnet2.radishsystems.com/ivr/api/")
            };
            {
                try
                {
                    Environment.SetEnvironmentVariable("UseAwsSms", "true");
                    var workflow = new CreateSessionWorkflow(testClient, awsSmsWorkflow: smsWorkflow);
                    var response = await workflow.Process(connectEventWithSkipNumberCheckSet, context);

                    Assert.Equal(JTokenType.Boolean, response["LambdaResult"].Type);
                    Assert.True((bool)response["LambdaResult"]);
                    Assert.Equal(QueryUrl, response["QueryUrl"].Value<string>());
                    Assert.False(response.TryGetValue("SessionStatus", out var value));
                    Assert.False(response.TryGetValue("SessionUrl", out value));
                    Assert.False(response.TryGetValue("PropertiesUrl", out value));
                    Assert.False(response.TryGetValue("ControlMessageUrl", out value));
                    Assert.True(response.TryGetValue("ConnectStartTime", out value));
                    Assert.Equal(JTokenType.Date, value.Type);
                }
                finally
                {
                    Environment.SetEnvironmentVariable("UseAwsSms", "false");
                }
            }
        }
    }
}