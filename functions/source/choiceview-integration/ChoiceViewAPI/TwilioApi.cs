using System;
using System.Net.Http;
using Refit;
using Twilio;

namespace ChoiceViewAPI
{
    public class TwilioApi
    {
        // Environment variables
        private const string ACCOUNTSID = "TWILIO_ACCOUNTSID";
        private const string AUTHTOKEN = "TWILIO_AUTHTOKEN";
        private const string PHONENUMBER = "TWILIO_PHONENUMBER";

        // Refit API interfaces
        public ITwilioMessagingApi MessagingApi;
        public ITwilioLookupsApi LookupsApi;

        // Twilio properties
        public string AccountSid;
        public string SmsNumber;

        // Twilio configuration
        private const string MESSAGINGURL = "https://api.twilio.com";
        private const string LOOKUPSURL = "https://lookups.twilio.com/v1";

        public TwilioApi()
        {
            var accountSid = Environment.GetEnvironmentVariable(ACCOUNTSID);
            var authToken = Environment.GetEnvironmentVariable(AUTHTOKEN);
            var phoneNumber = Environment.GetEnvironmentVariable(PHONENUMBER);

            if (string.IsNullOrWhiteSpace(accountSid) ||
                string.IsNullOrWhiteSpace(phoneNumber) ||
                string.IsNullOrWhiteSpace(authToken))
            {
                throw new Exception("Environment variables not set");
            }

            var twilioAuthentication = new TwilioAuthorization(accountSid, authToken);

            var messagingClient = new HttpClient
            {
                BaseAddress = new Uri(MESSAGINGURL),
                DefaultRequestHeaders = { Authorization = twilioAuthentication.AuthorizationHeader }
            };
            MessagingApi = RestService.For<ITwilioMessagingApi>(messagingClient);

            var lookupsClient = new HttpClient
            {
                BaseAddress = new Uri(LOOKUPSURL),
                DefaultRequestHeaders = { Authorization = twilioAuthentication.AuthorizationHeader }
            };
            LookupsApi = RestService.For<ITwilioLookupsApi>(lookupsClient);

            AccountSid = accountSid;
            SmsNumber = phoneNumber;
        }

        public TwilioApi(ITwilioLookupsApi lookupsApi, ITwilioMessagingApi messagingApi,
            string accountSid, string smsNumber)
        {
            LookupsApi = lookupsApi ?? throw new ArgumentNullException(nameof(lookupsApi));
            MessagingApi = messagingApi ?? throw new ArgumentNullException(nameof(messagingApi));
            AccountSid = accountSid ?? throw new ArgumentNullException(nameof(accountSid));
            SmsNumber = smsNumber ?? throw new ArgumentNullException(nameof(smsNumber));
        }
    }
}