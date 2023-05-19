using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Pinpoint;
using Amazon.Pinpoint.Model;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Newtonsoft.Json.Linq;

namespace ChoiceViewAPI;

public class AwsSmsWorkflow
{
    private readonly AmazonSimpleNotificationServiceClient awsClient;
    private readonly AmazonPinpointClient validationClient;
    
    public AwsSmsWorkflow()
    {
        awsClient = new AmazonSimpleNotificationServiceClient();
        validationClient = new AmazonPinpointClient();
    }

    public AwsSmsWorkflow(string accessKey, string secretKey)
    {
        awsClient = new AmazonSimpleNotificationServiceClient(accessKey, secretKey);
        validationClient = new AmazonPinpointClient(accessKey, secretKey);
    }

    public AwsSmsWorkflow(AWSCredentials credentials)
    {
        
        awsClient = new AmazonSimpleNotificationServiceClient(credentials);
        validationClient = new AmazonPinpointClient(credentials);
    }

    public AwsSmsWorkflow(AmazonSimpleNotificationServiceClient awsClient,
        AmazonPinpointClient validationClient)
    {
        this.awsClient = awsClient;
        this.validationClient = validationClient;
    }
    
    public async Task<JObject> Process(JObject connectEvent, ILambdaContext context)
    {
        dynamic result =  new JObject();

        var customerNumber =
            (string)connectEvent.SelectToken("Details.ContactData.CustomerEndpoint.Address")!;
        var customerNumberType = (string)connectEvent.SelectToken("Details.ContactData.CustomerEndpoint.Type")!;
        var systemNumber = (string)connectEvent.SelectToken("Details.ContactData.SystemEndpoint.Address");
        var message = (string)connectEvent.SelectToken("Details.Parameters.SmsMessage");
        var systemNumberType = (string)connectEvent.SelectToken("Details.ContactData.SystemEndpoint.Type");

        if (customerNumberType.Equals("TELEPHONE_NUMBER") && systemNumberType.Equals("TELEPHONE_NUMBER"))
        {
            var requestName = (string) connectEvent.SelectToken("Details.Parameters.RequestName") ?? "(null)";
            if (requestName.Equals("CreateSessionWithSms"))
            {
                var phoneNumber = $"phone={IVRWorkflow.SwitchCallerId(customerNumber)}";
                try
                {
                    var clientUrl = new UriBuilder(Environment.GetEnvironmentVariable("CHOICEVIEW_CLIENTURL") ??
                                                   "https://choiceview.com/secure.html")
                    {
                        Query = phoneNumber
                    };
                        
                    if (string.IsNullOrWhiteSpace(message)) 
                        message = $"Tap this link to start ChoiceView: {clientUrl.Uri}";
                        
                    if(!message.Contains(phoneNumber) && !message.EndsWith("phone="))
                        message += $" Tap this link to start ChoiceView: {clientUrl.Uri}";
                }
                catch (UriFormatException e)
                {
                    context.Logger.LogLine($"Cannot create the ChoiceView client uri: {e.Message}");
                    
                    if(string.IsNullOrWhiteSpace(message) ||
                       (!message.Contains(phoneNumber) && !message.EndsWith("phone=")))
                        return new JObject(new JProperty("LambdaResult", false));
                }
            }

            // Now actually send the message.
            var request = new PublishRequest
            {
                Message = message,
                PhoneNumber = customerNumber,
            };
            var skipNumberCheck = connectEvent.SelectToken("Details.Parameters.SkipNumberCheck");
            if (skipNumberCheck == null || !(bool)skipNumberCheck)
            {
                try
                {
                    var validateResponse = await PhoneNumberValidate(customerNumber);
                    if (!validateResponse.PhoneType.Equals("mobile", StringComparison.InvariantCultureIgnoreCase))
                    {
                        context.Logger.LogError("Cannot send SMS to a landline or VOIP telephone number.");
                        result.LambdaResult = false;
                        result.FailureReason = "Cannot send SMS to a landline or VOIP telephone number.";
                        return result;
                    }
                }
                catch (AmazonPinpointException ex)
                {
                    context.Logger.LogError($"Cannot validate customer number: {ex.Message}");
                    result.LambdaResult = false;
                    result.FailureReason = ex.Message;
                    return result;
                }
            }
            try
            {
                var response = await awsClient.PublishAsync(request);
                if (!string.IsNullOrEmpty(response.MessageId))
                {
                    context.Logger.LogInformation($"Sent SMS to customer: Message Id {response.MessageId}");
                    result.LambdaResult = true;
                }
                return result;
            }
            catch (AmazonSimpleNotificationServiceException ex)
            {
                context.Logger.LogError($"Cannot send SMS: {ex.Message}");
                result.LambdaResult = false;
                result.FailureReason = ex.Message;
                return result;
            }
        }
        
        var errorMessage = !customerNumberType.Equals("TELEPHONE_NUMBER")
            ? "Customer address"
            : "System address";
        var failureReason = $"Cannot send SMS: {errorMessage} is not a valid telephone number.";
        context.Logger.LogError($"Cannot send SMS: {errorMessage} is not a valid telephone number.");
        result.FailureReason = failureReason;
        result.LambdaResult = false;
        return result;
    }

    private async Task<NumberValidateResponse> PhoneNumberValidate(string? phoneNumber)
    {
        var request = new PhoneNumberValidateRequest
        {
            NumberValidateRequest = new NumberValidateRequest
            {
                PhoneNumber = phoneNumber,
                IsoCountryCode = "US"
            }
        };

        var response = await validationClient.PhoneNumberValidateAsync(request);
        return response.NumberValidateResponse;
    }

    private async Task<SendMessagesResponse> SendMessage(string phoneNumber, string message)
    {
        var config = new AddressConfiguration
        {
            ChannelType = ChannelType.SMS,
            BodyOverride = message
        };
        var numbers = new Dictionary<string, AddressConfiguration>
        {
            { phoneNumber, config }
        };
        
        var request = new SendMessagesRequest()
        {
            ApplicationId = "66588ea23a1f4d338aae7d78fb85a88d",
            MessageRequest = new MessageRequest
            {
                Addresses = numbers
            }
        };

        var response = await validationClient.SendMessagesAsync(request);
        
        return response;
    }
}