using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Newtonsoft.Json.Linq;

namespace ChoiceViewAPI
{
    public abstract class IVRWorkflow
    {
        protected readonly HttpClient _ApiClient;

        protected IVRWorkflow(HttpClient apiClient)
        {
            _ApiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        }

        public abstract Task<JObject> Process(JObject connectEvent, ILambdaContext context);

        protected void RequestException(Exception ex, string message, dynamic result, ILambdaContext context)
        {
            var errorMessage = message + ex.Message;
            context.Logger.LogLine(errorMessage);
            result.LambdaResult = false;
            result.FailureReason = errorMessage;
        }

        protected async Task RequestFailed(HttpResponseMessage response, dynamic result, ILambdaContext context)
        {
            var errorMessage = await ResponseFailureMessage(response);
            result.FailureReason = errorMessage;
            result.StatusCode = response.StatusCode;
            result.Timeout = response.StatusCode == HttpStatusCode.RequestTimeout;
            context.Logger.LogLine($"Request failed - ({response.StatusCode}) {errorMessage}");
            if (response.StatusCode == HttpStatusCode.NotFound) result.SessionStatus = "disconnected";
        }

        protected async Task<string> ResponseFailureMessage(HttpResponseMessage response)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            var failureReason = JObject.Parse(responseContent).Value<string>("message");
            return string.IsNullOrWhiteSpace(failureReason) ? response.ReasonPhrase : failureReason;
        }

        public static string SwitchCallerId(string callerId)
        {
            var cId = callerId.StartsWith("+") ? callerId.Substring(1) : callerId;

            // Switch can only handle 10 digit caller ids, so use the last 10 digits
            var len = cId.Length;
            return (len > 10) ? cId.Substring(len - 10) : cId;
        }

        protected string MakeRelativeUri(string href)
        {
            return _ApiClient.BaseAddress.MakeRelativeUri(new Uri(href, UriKind.Absolute)).ToString();
        }

        protected void AddPropertiesToResult(dynamic result, Dictionary<string, string> payload)
        {
            foreach (var property in payload)
            {
                result[property.Key] = property.Value;
            }
        }

        protected void AddSessionToResult(dynamic result, SessionResource session)
        {
            foreach (var link in session.Links)
            {
                if (link.Rel.Equals(Link.SelfRel)) { result.SessionUrl = MakeRelativeUri(link.Href); continue; }
                if (link.Rel.Equals(Link.PayloadRel)) { result.PropertiesUrl = MakeRelativeUri(link.Href); continue; }
                if (link.Rel.Equals(Link.ControlMessageRel)) { result.ControlMessageUrl = MakeRelativeUri(link.Href); }
            }
            AddPropertiesToResult(result, session.Properties);
        }
    }
}