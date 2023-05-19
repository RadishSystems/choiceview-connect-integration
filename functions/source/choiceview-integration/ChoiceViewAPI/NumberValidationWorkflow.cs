using System.Threading.Tasks;
using Amazon;
using Amazon.Lambda.Core;
using Amazon.Pinpoint;
using Amazon.Pinpoint.Model;
using Newtonsoft.Json.Linq;
using AmazonPinpointException = Amazon.Pinpoint.AmazonPinpointException;

namespace ChoiceViewAPI;

public class NumberValidationWorkflow
{
    private readonly AmazonPinpointClient awsClient;

    public NumberValidationWorkflow()
    {
        awsClient = new AmazonPinpointClient();
    }

    public async Task<JObject> Process(JObject connectEvent, ILambdaContext context)
    {
        dynamic result =  new JObject();
        var customerNumber =
            (string)connectEvent.SelectToken("Details.ContactData.CustomerEndpoint.Address");

        if (string.IsNullOrWhiteSpace(customerNumber))
        {
            const string failureReason = $"No customer number to validate";
            context.Logger.LogError(failureReason);
            result.LambdaResult = false;
            result.FailureReason = failureReason;
            return result;
        }
        
        var request = new PhoneNumberValidateRequest
        {
            NumberValidateRequest =
            {
                PhoneNumber = customerNumber,
                IsoCountryCode = "US"
            }
        };

        try
        {
            var response = await awsClient.PhoneNumberValidateAsync(request);
            result.NumberType = response.NumberValidateResponse.PhoneType;
            result.LambdaResult = true;
            return result;
        }
        catch (AmazonPinpointException ex)
        {
            context.Logger.LogError($"Cannot validate phone number - {ex.Message}");
            result.LambdaResult = false;
            result.FailureReason = ex.Message;
            return result;
        }
    }
}