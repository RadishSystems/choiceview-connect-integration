using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChoiceViewAPI
{
    public class SendUrlWorkflow : IVRWorkflow
    {
        public SendUrlWorkflow(HttpClient apiClient) : base(apiClient) { }

        public override async Task<JObject> Process(JObject connectEvent, ILambdaContext context)
        {
            dynamic result = new JObject();
            try
            {
                var sessionUrl = (string)connectEvent.SelectToken("Details.ContactData.Attributes.SessionUrl");
                var clientUrl = (string)connectEvent.SelectToken("Details.Parameters.ClientUrl");
                var noSessionUrl = string.IsNullOrWhiteSpace(sessionUrl);
                var noClientUrl = string.IsNullOrWhiteSpace(clientUrl);
                if (noSessionUrl || noClientUrl)
                {
                    var failureReason = noSessionUrl ? "No session url parameter" : "No client url parameter";
                    context.Logger.LogLine($"SendUrl - {failureReason}");
                    result.LambdaResult = false;
                    result.FailureReason = failureReason;
                    return result;
                }

                context.Logger.LogLine($"SendUrl request for {sessionUrl}, url {clientUrl}");

                using (var response = await _ApiClient.PostAsync(new Uri(sessionUrl, UriKind.Relative),
                    new StringContent(new JObject(new JProperty("url", clientUrl)).ToString(), Encoding.UTF8,
                        "application/json")))
                {
                    result.LambdaResult = response.IsSuccessStatusCode;
                    if (!response.IsSuccessStatusCode)
                    {
                        await RequestFailed(response, result, context);
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                RequestException(ex, "Error occurred when making API request - ", result, context);
            }
            catch (ArgumentException ex)
            {
                RequestException(ex, "Argument error - ", result, context);
            }
            catch (UriFormatException ex)
            {
                RequestException(ex, "Bad session uri - ", result, context);
            }
            catch (JsonException ex)
            {
                RequestException(ex, "Error reading the Connect event - ", result, context);
            }

            context.Logger.LogLine("SendUrl - result:\n" + result);
            return result;
        }
    }
}