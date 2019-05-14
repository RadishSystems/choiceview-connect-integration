using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChoiceViewAPI
{
    public class GetSessionWorkflow : IVRWorkflow
    {
        public GetSessionWorkflow(HttpClient apiClient) : base(apiClient) { }

        public override async Task<JObject> Process(JObject connectEvent, ILambdaContext context)
        {
            dynamic result = new JObject();
            try
            {
                var sessionUrl = (string)connectEvent.SelectToken("Details.ContactData.Attributes.SessionUrl");
                if (string.IsNullOrWhiteSpace(sessionUrl))
                {
                    var failureReason = "No session url parameter";
                    context.Logger.LogLine("GetSession - " + failureReason);
                    result.LambdaResult = false;
                    result.FailureReason = failureReason;
                    return result;
                }

                context.Logger.LogLine("GetSession request for " + sessionUrl);

                using (var response = await _ApiClient.GetAsync(new Uri(sessionUrl, UriKind.Relative)))
                {
                    result.LambdaResult = response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound;
                    if (response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            var session =
                                JsonConvert.DeserializeObject<SessionResource>(await response.Content.ReadAsStringAsync());
                            result.SessionStatus = session.Status;
                            AddPropertiesToResult(result, session.Properties);
                        }
                        else
                        {
                            context.Logger.LogLine($"GetSession - no session information received - status code {response.StatusCode}");
                            result.SessionStatus = string.Empty;
                            result.StatusCode = response.StatusCode;
                        }
                    }
                    else if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        context.Logger.LogLine($"GetSession - status code {response.StatusCode}, assume session is disconnected");
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

            context.Logger.LogLine("GetSession - result:\n" + result);
            return result;
        }
    }
}