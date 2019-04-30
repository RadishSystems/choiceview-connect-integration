
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Newtonsoft.Json.Linq;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace ChoiceViewAPI
{
    public class Function
    {
        private readonly LookupWorkflow getPhoneNumberType;
        private readonly SmsWorkflow sendSms;

        private readonly ChoiceViewSwitch choiceview = new ChoiceViewSwitch();
        private readonly TwilioApi twilioApi;
        private readonly CreateSessionWorkflow createSession;
        private readonly CreateSessionWorkflow createSessionWithSms;
        private readonly GetSessionWorkflow getSession;
        private readonly QuerySessionWorkflow querySession;
        private readonly EndSessionWorkflow endSession;
        private readonly SendUrlWorkflow sendUrl;
        private readonly GetControlMessageWorkflow getControlMessage;
        private readonly ClearControlMessageWorkflow clearControlMessage;
        private readonly TransferSessionWorkflow transferSession;
        private readonly AddPropertyWorkflow addProperty;
        private readonly GetPropertiesWorkflow getProperties;

        public bool TwilioValid => twilioApi.LookupsApi != null && twilioApi.MessagingApi != null;
        public bool ChoiceViewValid => choiceview.Valid;

        public Function()
        {
            twilioApi = new TwilioApi();

            if (TwilioValid)
            {
                getPhoneNumberType = new LookupWorkflow(twilioApi);
                sendSms = new SmsWorkflow(twilioApi);
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
            if (TwilioValid && ChoiceViewValid)
            {
                createSessionWithSms = new CreateSessionWorkflow(choiceview.ApiClient, sendSms);
            }
        }

        public async Task<JObject> FunctionHandler(JObject connectEvent, ILambdaContext context)
        {
            context.Logger.LogLine("Connect event:\n" + connectEvent);

            var requestName = (string) connectEvent.SelectToken("Details.Parameters.RequestName") ?? "(null)";

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
                    return await (getPhoneNumberType != null ? getPhoneNumberType.Process(connectEvent, context) : invalidApiError);
                case "SendSms":
                    return await (sendSms != null ? sendSms.Process(connectEvent, context) : invalidApiError);
                case "CreateSession":
                    return await (createSession != null ? createSession.Process(connectEvent, context) : invalidApiError);
                case "CreateSessionWithSms":
                    return await (createSessionWithSms != null ? createSessionWithSms.Process(connectEvent, context) : invalidApiError);
                case "GetSession":
                    return await (getSession != null ? getSession.Process(connectEvent, context) : invalidApiError);
                case "TransferSession":
                    return await (transferSession != null ? transferSession.Process(connectEvent, context) : invalidApiError);
                case "QuerySession":
                    return await (querySession != null ? querySession.Process(connectEvent, context) : invalidApiError);
                case "EndSession":
                    return await (endSession != null ? endSession.Process(connectEvent, context) : invalidApiError);
                case "SendUrl":
                    return await (sendUrl != null ? sendUrl.Process(connectEvent, context) : invalidApiError);
                case "GetControlMessage":
                    return await (getControlMessage != null ? getControlMessage.Process(connectEvent, context) : invalidApiError);
                case "ClearControlMessage":
                    return await (clearControlMessage != null ? clearControlMessage.Process(connectEvent, context) : invalidApiError);
                case "AddProperty":
                    return await (addProperty != null ? addProperty.Process(connectEvent, context) : invalidApiError);
                case "GetProperties":
                    return await (getProperties != null ? getProperties.Process(connectEvent, context) : invalidApiError);
                default:
                    context.Logger.LogLine("Unknown request " + requestName);
                    return new JObject(new JProperty("LambdaResult", false),
                        new JProperty("FailureReason", "Unknown request"));
            }
        }
    }
}
