using System;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChoiceViewAPI
{
    public class QuerySessionWorkflow : IVRWorkflow
    {
        public QuerySessionWorkflow(HttpClient apiClient) : base(apiClient)
        {
        }

        public override async Task<JObject> Process(JObject connectEvent, ILambdaContext context)
        {
            dynamic result = new JObject();
            try
            {
                var callId = (string)connectEvent.SelectToken("Details.ContactData.ContactId");
                var queryUrl = (string)connectEvent.SelectToken("Details.ContactData.Attributes.QueryUrl") ?? $"sessions?callid={callId}";
                var connectStartTime = (DateTime)connectEvent.SelectToken("Details.ContactData.Attributes.ConnectStartTime");

                context.Logger.LogLine($"QuerySession request for {callId}");

                using (var response = await _ApiClient.GetAsync(new Uri(queryUrl, UriKind.Relative)))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var session =
                            JsonConvert.DeserializeObject<SessionResource>(await response.Content.ReadAsStringAsync());
                        AddSessionToResult(result, session);
                        result.LambdaResult = true;
                        result.SessionRetrieved = true;
                        result.SessionTimeout = false;
                        result.SessionStatus = session.Status;
                        context.Logger.LogLine($"QuerySession - session {session.SessionId} found");
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        result.LambdaResult = true;
                        result.SessionRetrieved = false;
                        var connectDuration = DateTime.Now - connectStartTime;
                        result.SessionTimeout = connectDuration > TimeSpan.FromSeconds(90);
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
                RequestException(ex, "Bad query uri - ", result, context);
            }
            catch (JsonException ex)
            {
                RequestException(ex, "Error reading the Connect event - ", result, context);
            }

            context.Logger.LogLine("QuerySession - result:\n" + result);
            return result;
        }
    }
}