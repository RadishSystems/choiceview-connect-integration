using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Lambda.TestUtilities;
using Amazon.Pinpoint;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Amazon.SimpleNotificationService;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ChoiceViewAPI.Tests;

public class AwsSmsTests
{
  private static readonly JObject ConnectEventWithMessageEndingWithPhoneParameter = 
            JObject.Parse(@"{
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
        ""SmsMessage"": ""Tap this link to start ChoiceView: https://choiceview.com/secure.html?phone=""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
        private static readonly JObject ConnectEventWithMessageEndingWithPhoneParameterAndSkipNumberCheckSet = 
            JObject.Parse(@"{
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
        ""SmsMessage"": ""Tap this link to start ChoiceView: https://choiceview.com/secure.html?phone="",
        ""SkipNumberCheck"": ""true""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
        private static readonly JObject ConnectEventWithMessageWithClientUrl = 
            JObject.Parse(@"{
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
        ""SmsMessage"": ""Tap this link to start ChoiceView: https://choiceview.com/secure.html?phone=""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
        private static readonly JObject ConnectEventWithMessageWithoutClientUrl = 
            JObject.Parse(@"{
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
        ""SmsMessage"": ""Welcome to the Unit test!""
      }
    },
    ""Name"": ""ContactFlowEvent""
  }");
        
    private readonly TestLambdaContext _context;
    private readonly AWSCredentials _credentials;
    private readonly AmazonSecurityTokenServiceClient _tokenService;

    private readonly string _systemNumber = "+18582016694";
    private readonly string _customerNumber = "+17202950840";
    private readonly string _smsNumber = "+15005550006";

    public static IEnumerable<object[]> Events =>
      new List<object[]>
      {
        new object[] { ConnectEventWithMessageEndingWithPhoneParameter },
        new object[] { ConnectEventWithMessageWithClientUrl }
      };

    public AwsSmsTests(ITestOutputHelper output)
    {
        _context = new TestLambdaContext
        {
            Logger = new XUnitLambaLogger(output)
        };

        var profileStore = new CredentialProfileStoreChain();
        if (!profileStore.TryGetAWSCredentials("connect-development", out _credentials))
        {
          _credentials = new AnonymousAWSCredentials();
        }

        _tokenService = new AmazonSecurityTokenServiceClient(_credentials);
    }

    [Fact]
    public async Task SmsReturnsFalseIfValidButBadCredentials()
    {
      var connectFunction = new AwsSmsWorkflow("BadAccessKey", "BadSecret");
      var result = await connectFunction.Process(ConnectEventWithMessageEndingWithPhoneParameter, _context);

      var lambdaResult = result["LambdaResult"];
      Assert.NotNull(lambdaResult);
      Assert.True(lambdaResult.Type == JTokenType.Boolean);
      Assert.False(lambdaResult.Value<bool>());
    }

    [Fact]
    public async Task SmsReturnsTrueIfValidCredentialsAndMessageQueued()
    {
      var testSnsClient = new Mock<AmazonSimpleNotificationServiceClient>();
      var testPinpointClient = new Mock<AmazonPinpointClient>();

      // var tokenResponse = await _tokenService.AssumeRoleAsync(new AssumeRoleRequest
      // {
      //   DurationSeconds = 900
      // });
      
      var connectFunction = new AwsSmsWorkflow(_credentials);
      var result = await connectFunction.Process(ConnectEventWithMessageEndingWithPhoneParameter, _context);
      
      Assert.Single(result);
      var lambdaResult = result["LambdaResult"];
      Assert.NotNull(lambdaResult);
      Assert.True(lambdaResult.Type == JTokenType.Boolean, "LambdaResult type is not Boolean");
      Assert.True(lambdaResult.Value<bool>(), "LambdaResult value is not true");
    }
}