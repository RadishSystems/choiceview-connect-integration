using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChoiceViewAPI
{
    public class GetPropertiesWorkflow : IVRWorkflow
    {
        public GetPropertiesWorkflow(HttpClient apiClient) : base(apiClient) { }

        public override async Task<JObject> Process(JObject connectEvent, ILambdaContext context)
        {
            dynamic result = new JObject();
            try
            {
                var propertiesUrl = (string)connectEvent.SelectToken("Details.ContactData.Attributes.PropertiesUrl");
                if (string.IsNullOrWhiteSpace(propertiesUrl))
                {
                    var failureReason = "No properties url attribute";
                    context.Logger.LogLine("GetProperties - " + failureReason);
                    result.LambdaResult = false;
                    result.FailureReason = failureReason;
                    return result;
                }

                context.Logger.LogLine("GetProperties request for " + propertiesUrl);

                using (var response = await _ApiClient.GetAsync(new Uri(propertiesUrl, UriKind.Relative)))
                {
                    result.LambdaResult = response.IsSuccessStatusCode;
                    if (response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            var properties =
                                JsonConvert.DeserializeObject<PropertiesResource>(await response.Content.ReadAsStringAsync());
                            AddPropertiesToResult(result, properties.Properties);
                        }
                        else
                        {
                            context.Logger.LogLine($"GetProperties - no property information received - status code {response.StatusCode}");
                            result.StatusCode = response.StatusCode;
                        }
                    }
                    else if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        context.Logger.LogLine($"GetProperties - status code {response.StatusCode}, assume session is disconnected");
                        result.StatusCode = response.StatusCode;
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
                RequestException(ex, "Bad properties uri - ", result, context);
            }
            catch (JsonException ex)
            {
                RequestException(ex, "Error reading the Connect event - ", result, context);
            }

            context.Logger.LogLine("GetProperties - result:\n" + result);
            return result;
        }
    }
}