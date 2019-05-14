using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Newtonsoft.Json.Linq;
using Refit;

namespace ChoiceViewAPI
{
    public abstract class TwilioWorkflow
    {
        protected TwilioApi _twilioApi;

        protected TwilioWorkflow(TwilioApi twilioApi)
        {
            _twilioApi = twilioApi;
        }

        public abstract Task<JObject> Process(JObject connectEvent, ILambdaContext context);

        protected async Task<string> GetPhoneNumberType(string customerNumber, ILambdaLogger logger)
        {
            string numberType;
            try
            {
                dynamic info = await _twilioApi.LookupsApi.NumberInfo(customerNumber, "USA", "carrier");
                numberType = info.carrier.type;
                logger.LogLine($"PhoneNumber '{customerNumber}' is type '{numberType}'");
            }
            catch (ApiException ex)
            {
                logger.LogLine("PhoneNumber lookup failed: " + ex.Message);
                logger.LogLine(ex.Content ?? "(no exception data)");
                numberType = string.Empty;
            }
            return numberType;
        }
    }
}