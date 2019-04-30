using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChoiceViewAPI
{
    public class GetControlMessageWorkflow : IVRWorkflow
    {
        public GetControlMessageWorkflow(HttpClient apiClient) : base(apiClient) { }

        public override async Task<JObject> Process(JObject connectEvent, ILambdaContext context)
        {
            dynamic result = new JObject();
            try
            {
                var controlMessageUrl = (string)connectEvent.SelectToken("Details.ContactData.Attributes.ControlMessageUrl");
                if (string.IsNullOrWhiteSpace(controlMessageUrl))
                {
                    var failureReason = "No control message url parameter";
                    context.Logger.LogLine($"GetControlMessage - {failureReason}");
                    result.LambdaResult = false;
                    result.FailureReason = failureReason;
                    return result;
                }

                context.Logger.LogLine($"GetControlMessage request for {controlMessageUrl}");

                using (var response = await _ApiClient.GetAsync(new Uri(controlMessageUrl, UriKind.Relative)))
                {
                    result.LambdaResult = response.IsSuccessStatusCode;
                    if (response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            result.ControlMessageAvailable = true;
                            var controlMsg =
                                JsonConvert.DeserializeObject<Dictionary<string,string>>(await response.Content.ReadAsStringAsync());
                            context.Logger.LogLine("GetControlMessage - recieved control message:");
                            foreach (var formElement in controlMsg)
                            {
                                result[formElement.Key] = formElement.Value;
                                context.Logger.LogLine("key: " + formElement.Key + ", value: " + formElement.Value);
                            }
                        }
                        else if (response.StatusCode == HttpStatusCode.NoContent)
                        {
                            result.ControlMessageAvailable = false;
                            context.Logger.LogLine("GetControlMessage - no control message available" );
                        }
                        else
                        {
                            context.Logger.LogLine("GetControlMessage - no control message received - status code " + response.StatusCode);
                            result.StatusCode = response.StatusCode;
                        }
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
                RequestException(ex, "Bad control message uri - ", result, context);
            }
            catch (JsonException ex)
            {
                RequestException(ex, "Error reading the Connect event - ", result, context);
            }

            context.Logger.LogLine("GetControlMessage - result:\n" + result);
            return result;
        }
    }
}