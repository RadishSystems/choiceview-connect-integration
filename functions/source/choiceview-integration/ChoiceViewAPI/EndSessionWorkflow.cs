using System;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChoiceViewAPI
{
    public class EndSessionWorkflow : IVRWorkflow
    {
        public EndSessionWorkflow(HttpClient apiClient) : base(apiClient) { }

        public override async Task<JObject> Process(JObject connectEvent, ILambdaContext context)
        {
            dynamic result = new JObject();
            try
            {
                var sessionUrl = (string)connectEvent.SelectToken("Details.ContactData.Attributes.SessionUrl");
                if (string.IsNullOrWhiteSpace(sessionUrl))
                {
                    var failureReason = "No session url parameter";
                    context.Logger.LogLine($"EndSession - {failureReason}");
                    result.LambdaResult = false;
                    result.FailureReason = failureReason;
                    return result;
                }

                context.Logger.LogLine("EndSession request for " + sessionUrl);

                using (var response = await _ApiClient.DeleteAsync(new Uri(sessionUrl, UriKind.Relative)))
                {
                    result.LambdaResult = response.IsSuccessStatusCode;
                    if (response.IsSuccessStatusCode)
                    {
                        result.SessionStatus = "disconnected";
                    }
                    else
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

            context.Logger.LogLine("EndSession - result:\n" + result);
            return result;
        }
    }
}