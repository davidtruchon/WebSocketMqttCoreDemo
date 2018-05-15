using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using AWSSignatureV4_S3_Sample.Signers;
using AWSSignatureV4_S3_Sample.Util;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Diagnostics;
using MQTTnet.Serializer;

namespace AwsWebsocketCore
{
    class Program
    {
        public static string ACCESS_KEY = "MY_AWS_ACCESS_KEY";
        public static string SECRET_KEY = "MY_AWS_SECRET_KEY";

        public static string Endpoint = "wss://a3utcui8num4k2.iot.us-east-1.amazonaws.com/mqtt";
        static MqttNetLogger logger;

        static void Main(string[] args)
        {
            WebsocketTest();
            Console.ReadLine();
        }
        static void WebsocketTest()
        {
            logger = new MqttNetLogger();
            logger.LogMessagePublished += LoggerOnLogMessagePublished;
            var endpoint = Endpoint;
            var queryParams = new StringBuilder();
            var expiresOn = DateTime.UtcNow.AddDays(2);
            var period = Convert.ToInt64((expiresOn.ToUniversalTime() - DateTime.UtcNow).TotalSeconds);
            queryParams.AppendFormat("{0}={1}", AWS4SignerBase.X_Amz_Expires, HttpHelpers.UrlEncode(period.ToString()));

            var headers = new Dictionary<string, string> { };
            var signer = new AWS4SignerForQueryParameterAuth()
            {
                EndpointUri = new Uri(endpoint),
                HttpMethod = "GET",
                Region = "us-east-1",
                Service = "iotdevicegateway"
            };

            var authorization = signer.ComputeSignature(headers,
                queryParams.ToString(),
                AWS4SignerBase.EMPTY_BODY_SHA256,
                ACCESS_KEY,
                SECRET_KEY);

            var urlBuilder = new StringBuilder(endpoint.ToString());
            urlBuilder.AppendFormat("?{0}", queryParams.ToString());
            urlBuilder.AppendFormat("&{0}", authorization);
            var presignedUrl = urlBuilder.ToString();

            Task.Run(() => ConnectWs(presignedUrl));
        }

        private static async void ConnectWs(string presignedurl)
        {
            var client = new MqttFactory().CreateMqttClient(logger);
            var options = new MqttClientOptionsBuilder()
                .WithWebSocketServer(presignedurl)
                .WithProtocolVersion(MqttProtocolVersion.V311)
                .Build();
            (options.ChannelOptions as MqttClientWebSocketOptions).SubProtocols = new List<string>();
            (options.ChannelOptions as MqttClientWebSocketOptions).SubProtocols.Add("mqtt");
            MqttClientConnectResult res = await client.ConnectAsync(options);
        }
        private static void LoggerOnLogMessagePublished(object sender, MqttNetLogMessagePublishedEventArgs mqttNetLogMessagePublishedEventArgs)
        {
            Console.WriteLine(mqttNetLogMessagePublishedEventArgs.TraceMessage.Message);
        }
    }
}
