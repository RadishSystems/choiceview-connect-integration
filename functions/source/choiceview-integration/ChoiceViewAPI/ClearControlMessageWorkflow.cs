using System;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChoiceViewAPI
{
    public class ClearControlMessageWorkflow : IVRWorkflow
    {
        public ClearControlMessageWorkflow(HttpClient apiClient) : base(apiClient) { }

        public override async Task<JObject> Process(JObject connectEvent, ILambdaContext context)
        {
            dynamic result = new JObject();
            try
            {
                var url = (string)connectEvent.SelectToken("Details.ContactData.Attributes.ControlMessageUrl");
                if (string.IsNullOrWhiteSpace(url))
                {
                    var failureReason = "No control message url parameter";
                    context.Logger.LogLine("ClearControlMessage - " + failureReason);
                    result.LambdaResult = false;
                    result.FailureReason = failureReason;
                    return result;
                }

                context.Logger.LogLine("ClearControlMessage request for " + url);

                using (var response = await _ApiClient.DeleteAsync(new Uri(url, UriKind.Relative)))
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
                RequestException(ex, "Bad control message uri - ", result, context);
            }
            catch (JsonException ex)
            {
                RequestException(ex, "Error reading the Connect event - ", result, context);
            }

            context.Logger.LogLine("ClearControlMessage - result:\n" + result);
            return result;
        }
    }
}