using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Newtonsoft.Json.Linq;

namespace ChoiceViewAPI
{
    public class LookupWorkflow : TwilioWorkflow
    {
        public LookupWorkflow(TwilioApi twilioApi) : base(twilioApi) { }

        public override async Task<JObject> Process(JObject connectEvent, ILambdaContext context)
        {
            var customerNumber =
                (string)connectEvent.SelectToken("Details.ContactData.CustomerEndpoint.Address");

            var numberType = await GetPhoneNumberType(customerNumber, context.Logger);

            return new JObject(new JProperty("NumberType", numberType), new JProperty("LambdaResult", true));
        }
    }
}