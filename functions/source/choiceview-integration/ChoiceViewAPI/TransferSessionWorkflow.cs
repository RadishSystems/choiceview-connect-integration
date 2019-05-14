using System;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChoiceViewAPI
{
    public class TransferSessionWorkflow : IVRWorkflow
    {
        public TransferSessionWorkflow(HttpClient apiClient) : base(apiClient) { }

        public override async Task<JObject> Process(JObject connectEvent, ILambdaContext context)
        {
            dynamic result = new JObject();
            try
            {
                var sessionUrl = (string)connectEvent.SelectToken("Details.ContactData.Attributes.SessionUrl");
                var accountId = (string)connectEvent.SelectToken("Details.Parameters.AccountId");
                var noSessionUrl = string.IsNullOrWhiteSpace(sessionUrl);
                var noAccountId = string.IsNullOrWhiteSpace(accountId);
                if (noSessionUrl || noAccountId)
                {
                    var failureReason = noSessionUrl ? "No session url parameter" : "No account id parameter";
                    context.Logger.LogLine($"TransferSession - {failureReason}");
                    result.LambdaResult = false;
                    result.FailureReason = failureReason;
                    return result;
                }

                context.Logger.LogLine($"TransferSession request for {sessionUrl}, account id {accountId}");
                using (var response = await _ApiClient.PostAsync(new Uri(sessionUrl + $"/transfer/{accountId}", UriKind.Relative), null))
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
                RequestException(ex, "Bad transfer session uri - ", result, context);
            }
            catch (JsonException ex)
            {
                RequestException(ex, "Error reading the Connect event - ", result, context);
            }

            context.Logger.LogLine("TransferSession - result:\n" + result);
            return result;
        }
    }
}