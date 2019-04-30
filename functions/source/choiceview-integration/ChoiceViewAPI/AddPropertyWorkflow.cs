using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChoiceViewAPI
{
    public class AddPropertyWorkflow : IVRWorkflow
    {
        public AddPropertyWorkflow(HttpClient apiClient) : base(apiClient) { }

        public override async Task<JObject> Process(JObject connectEvent, ILambdaContext context)
        {
            dynamic result = new JObject();
            try
            {
                var propertiesUrl = (string)connectEvent.SelectToken("Details.ContactData.Attributes.PropertiesUrl");
                var propertyName = (string)connectEvent.SelectToken("Details.Parameters.PropertyName");
                var propertyValue = (string)connectEvent.SelectToken("Details.Parameters.PropertyValue");
                var noPropertiesUrl = string.IsNullOrWhiteSpace(propertiesUrl);
                var noProperty = string.IsNullOrWhiteSpace(propertyName);
                if (noPropertiesUrl || noProperty)
                {
                    var failureReason = noPropertiesUrl ? "No properties url attribute" : "No property name parameter";
                    context.Logger.LogLine($"AddProperty - {failureReason}");
                    result.LambdaResult = false;
                    result.FailureReason = failureReason;
                    return result;
                }

                context.Logger.LogLine($"Add property request for {propertiesUrl}, property {propertyName}:{propertyValue ?? "(null)"}");

                using (var response = await _ApiClient.PostAsync(new Uri(propertiesUrl, UriKind.Relative),
                    new StringContent(new JObject(new JProperty("name", propertyName), new JProperty("value", propertyValue)).ToString(),
                        Encoding.UTF8, "application/json")))
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
                RequestException(ex, "Bad properties uri - ", result, context);
            }
            catch (JsonException ex)
            {
                RequestException(ex, "Error reading the Connect event - ", result, context);
            }

            context.Logger.LogLine("AddProperty - result:\n" + result);
            return result;
        }
    }
}