using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChoiceViewAPI
{
    public class CreateSessionWorkflow : IVRWorkflow
    {
        private readonly SmsWorkflow smsWorkflow;

        public CreateSessionWorkflow(HttpClient apiClient, 
            SmsWorkflow smsWorkflow = null) : base(apiClient)
        {
            this.smsWorkflow = smsWorkflow;
        }

        public override async Task<JObject> Process(JObject connectEvent, ILambdaContext context)
        {
            dynamic result = new JObject();
            try
            {
                dynamic newSessionParameters = new JObject();
                var customerNumber =
                    (string)connectEvent.SelectToken("Details.ContactData.CustomerEndpoint.Address");
                var customerNumberType = (string)connectEvent.SelectToken("Details.ContactData.CustomerEndpoint.Type");

                if (customerNumberType.Equals("TELEPHONE_NUMBER"))
                {
                    var callerId = SwitchCallerId(customerNumber);
                    newSessionParameters.callerId = callerId;
                    newSessionParameters.callId = (string)connectEvent.SelectToken("Details.ContactData.ContactId");
                    newSessionParameters.immediateReturn = true;
                    context.Logger.LogLine("CreateSession request for " + callerId);
                }
                else
                {
                    var failureReason = "no caller id found";
                    context.Logger.LogLine($"CreateSession - {failureReason}");
                    result.LambdaResult = false;
                    result.FailureReason = failureReason;
                    return result;
                }

                if (smsWorkflow != null)
                {
                    var smsMessage = (string)connectEvent.SelectToken("Details.Parameters.SmsMessage");
                    if (!string.IsNullOrWhiteSpace(smsMessage))
                    {
                        context.Logger.LogLine("CreateSession - Attempt to send SMS with client url");
                        var smsResponse = await smsWorkflow.Process(connectEvent, context);
                        var smsSent = (bool)smsResponse["LambdaResult"];
                        result.SmsSent = smsSent;
                    }
                    else result.SmsSent = false;
                }

                using (var response = await _ApiClient.PostAsync("sessions",
                    new StringContent(newSessionParameters.ToString(), Encoding.UTF8, "application/json")))
                {
                    result.LambdaResult =
                        response.StatusCode == HttpStatusCode.Accepted ||
                        response.StatusCode == HttpStatusCode.Created;

                    if (response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == HttpStatusCode.Accepted)
                        {
                            var body = JObject.Parse(await response.Content.ReadAsStringAsync());
                            var queryUrl = (string) body["QueryUrl"];
                            result.ConnectStartTime = DateTime.Now;
                            result.QueryUrl = MakeRelativeUri(queryUrl);
                            context.Logger.LogLine("CreateSession - Session create started, return immediately");
                            context.Logger.LogLine($"CreateSession - absolute query url = '{queryUrl}', QueryUrl parameter = '{(string)result["QueryUrl"]}'");
                        }
                        else if (response.StatusCode == HttpStatusCode.Created)
                        {
                            var session =
                                JsonConvert.DeserializeObject<SessionResource>(
                                    await response.Content.ReadAsStringAsync());
                            AddSessionToResult(result, session);
                            result.SessionStatus = session.Status;
                            context.Logger.LogLine($"CreateSession - session {session.SessionId} created");
                        }
                        else await RequestFailed(response, result, context);
                    }
                    else await RequestFailed(response, result, context);
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

            context.Logger.LogLine("CreateSession - result:\n" + result);
            return result;
        }
    }
}