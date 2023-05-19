using System;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Newtonsoft.Json.Linq;
// ReSharper disable InconsistentNaming

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace ChoiceViewAPI
{
    public class Function
    {
        private readonly LookupWorkflow? getTwilioPhoneNumberType;
        private readonly TwilioSmsWorkflow? sendTwilioSms;

        private readonly NumberValidationWorkflow? getAwsPhoneNumberType;
        private readonly AwsSmsWorkflow? sendAwsSms;
        
        private readonly ChoiceViewSwitch choiceview = new ChoiceViewSwitch();
        private readonly TwilioApi? twilioApi;
        private readonly CreateSessionWorkflow? createSession;
        private readonly CreateSessionWorkflow? createSessionWithSms;
        private readonly GetSessionWorkflow? getSession;
        private readonly QuerySessionWorkflow? querySession;
        private readonly EndSessionWorkflow? endSession;
        private readonly SendUrlWorkflow? sendUrl;
        private readonly GetControlMessageWorkflow? getControlMessage;
        private readonly ClearControlMessageWorkflow? clearControlMessage;
        private readonly TransferSessionWorkflow? transferSession;
        private readonly AddPropertyWorkflow? addProperty;
        private readonly GetPropertiesWorkflow? getProperties;

        public bool TwilioValid => twilioApi is { LookupsApi: { }, MessagingApi: { } };
        public bool ChoiceViewValid => choiceview.Valid;
        private bool AwsMessagingValid;

        public Function()
        {
            AwsMessagingValid = Convert.ToBoolean(Environment.GetEnvironmentVariable("UseAwsSms"));
            if (AwsMessagingValid)
            {
                getAwsPhoneNumberType = new NumberValidationWorkflow();
                sendAwsSms = new AwsSmsWorkflow();
            }
            else
            {
                twilioApi = new TwilioApi();
                if (TwilioValid)
                {
                    getTwilioPhoneNumberType = new LookupWorkflow(twilioApi);
                    sendTwilioSms = new TwilioSmsWorkflow(twilioApi);
                }
            }

            if (ChoiceViewValid)
            {
                createSession = new CreateSessionWorkflow(choiceview.ApiClient);
                getSession = new GetSessionWorkflow(choiceview.ApiClient);
                transferSession = new TransferSessionWorkflow(choiceview.ApiClient);
                querySession = new QuerySessionWorkflow(choiceview.ApiClient);
                endSession = new EndSessionWorkflow(choiceview.ApiClient);
                sendUrl = new SendUrlWorkflow(choiceview.ApiClient);
                getControlMessage = new GetControlMessageWorkflow(choiceview.ApiClient);
                clearControlMessage = new ClearControlMessageWorkflow(choiceview.ApiClient);
                addProperty = new AddPropertyWorkflow(choiceview.ApiClient);
                getProperties = new GetPropertiesWorkflow(choiceview.ApiClient);
            }
            createSessionWithSms = new CreateSessionWorkflow(choiceview.ApiClient,
                sendTwilioSms, sendAwsSms);
        }

        public async Task<JObject> FunctionHandler(JObject connectEvent, ILambdaContext context)
        {
            context.Logger.LogLine("Connect event:\n" + connectEvent);

            var requestName = (string?) connectEvent.SelectToken("Details.Parameters.RequestName") ?? "(null)";

            dynamic invalidApiError = new JObject();
            if (!TwilioValid || !ChoiceViewValid)
            {
                invalidApiError.LambdaResult = false;
                invalidApiError.FailureResult =
                    ChoiceViewValid ? "Not connected to Twilio." : "Not connected to ChoiceView.";
            }

            switch (requestName)
            {
                case "GetPhoneNumberType":
                    if (getTwilioPhoneNumberType != null)
                        return await getTwilioPhoneNumberType.Process(connectEvent, context);
                    if (getAwsPhoneNumberType != null)
                        return await getAwsPhoneNumberType.Process(connectEvent, context);
                    return invalidApiError;
                case "SendSms":
                    if (sendTwilioSms != null)
                        return await sendTwilioSms.Process(connectEvent, context);
                    if (sendAwsSms != null)
                        return await sendAwsSms.Process(connectEvent, context);
                    return invalidApiError;
                case "CreateSession":
                    return await (createSession?.Process(connectEvent, context) ?? invalidApiError);
                case "CreateSessionWithSms":
                    return await (createSessionWithSms?.Process(connectEvent, context) ?? invalidApiError);
                case "GetSession":
                    return await (getSession?.Process(connectEvent, context) ?? invalidApiError);
                case "TransferSession":
                    return await (transferSession?.Process(connectEvent, context) ?? invalidApiError);
                case "QuerySession":
                    return await (querySession?.Process(connectEvent, context) ?? invalidApiError);
                case "EndSession":
                    return await (endSession?.Process(connectEvent, context) ?? invalidApiError);
                case "SendUrl":
                    return await (sendUrl?.Process(connectEvent, context) ?? invalidApiError);
                case "GetControlMessage":
                    return await (getControlMessage?.Process(connectEvent, context) ?? invalidApiError);
                case "ClearControlMessage":
                    return await (clearControlMessage?.Process(connectEvent, context) ?? invalidApiError);
                case "AddProperty":
                    return await (addProperty?.Process(connectEvent, context) ?? invalidApiError);
                case "GetProperties":
                    return await (getProperties?.Process(connectEvent, context) ?? invalidApiError);
                default:
                    context.Logger.LogLine("Unknown request " + requestName);
                    return new JObject(new JProperty("LambdaResult", false),
                        new JProperty("FailureReason", "Unknown request"));
            }
        }
    }
}
