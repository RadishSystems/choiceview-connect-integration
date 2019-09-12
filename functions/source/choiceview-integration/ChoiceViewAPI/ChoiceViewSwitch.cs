using System;
using System.Net.Http;
using System.Net.Http.Headers;
using Amazon.Lambda.Core;

namespace ChoiceViewAPI
{
    /// <summary>
    /// Encapsulates the HttpClient instance used to send API requests to the ChoiceView switch.
    /// Creates the HttpClient uing parameters from the Lambda environment, and sets up the authentication handler.
    /// Should be used as a singleton, only one instance per AWS Lambda function.
    /// All IVR workflows have a constructor that takes an HttpClient instance, use ApiClient property.
    /// </summary>
    public class ChoiceViewSwitch
    {
        public HttpClient ApiClient { get; }

        public bool Valid { get; }

        public Uri BaseUri { get; }

        // Intended for use during testing, or integrating with an IOC framework.
        // This also allows sending API requests without an authorization header.
        // Caller takes total responsibility for configuring the HttpClient instance to work correctly with the ChoiceView API server.
        public ChoiceViewSwitch(HttpClient apiClient)
        {
            ApiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            Valid = true;
            BaseUri = ApiClient.BaseAddress;
        }

        public ChoiceViewSwitch()
        {
            try
            {
                // CHOICEVIEW_SERVICEURL must have trailing slash - https://cvnet.radishsystems.com/ivr/api/
                var apiUrl = Environment.GetEnvironmentVariable("CHOICEVIEW_SERVICEURL");
                if (string.IsNullOrWhiteSpace(apiUrl))
                {
                    LambdaLogger.Log("ChoiceView service url not available from environment.");
                    Valid = false;
                }
                else
                {
                    BaseUri = new Uri(apiUrl);
                    ApiClient = new HttpClient(new ChoiceViewClientHandler(
                        Environment.GetEnvironmentVariable("CHOICEVIEW_CLIENTID"),
                        Environment.GetEnvironmentVariable("CHOICEVIEW_CLIENTSECRET")))
                    {
                        BaseAddress = BaseUri
                    };
                    ApiClient.DefaultRequestHeaders.Clear();
                    ApiClient.DefaultRequestHeaders.ConnectionClose = false;
                    ApiClient.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));
                    ApiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer");
                    Valid = true;
                }
                return;
            }
            catch (ArgumentException ex)
            {
                LambdaLogger.Log("ChoiceView API credentials not available from environment - " + ex.Message);
            }
            catch (UriFormatException ex)
            {
                LambdaLogger.Log("Invalid ChoiceView service url - " + ex.Message);
            }
            Valid = false;
        }
    }
}