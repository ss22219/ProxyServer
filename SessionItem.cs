using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Titanium.Web.Proxy.Http;

namespace Server
{
    public class SessionListItem
    {
        private long? bodySize;
        private Exception exception;
        private string host;
        private string process;
        private string protocol;
        private long receivedDataCount;
        private long sentDataCount;
        private string statusCode;
        private string url;

        public int Number { get; set; }

        public HttpWebClient WebSession { get; set; }

        public bool IsTunnelConnect { get; set; }

        public string StatusCode
        {
            get => statusCode;
            set => statusCode = value;
        }

        public string Protocol
        {
            get => protocol;
            set => protocol = value;
        }

        public string Host
        {
            get => host;
            set => host = value;
        }

        public string Url
        {
            get => url;
            set => url = value;
        }

        public long? BodySize
        {
            get => bodySize;
            set => bodySize = value;
        }

        public string Process
        {
            get => process;
            set => process = value;
        }

        public long ReceivedDataCount
        {
            get => receivedDataCount;
            set => receivedDataCount = value;
        }

        public long SentDataCount
        {
            get => sentDataCount;
            set => sentDataCount = value;
        }

        public Exception Exception
        {
            get => exception;
            set => exception = value;
        }

        public void Update()
        {
            var request = WebSession.Request;
            var response = WebSession.Response;
            int statusCode = response?.StatusCode ?? 0;
            StatusCode = statusCode == 0 ? "-" : statusCode.ToString();
            Protocol = request.RequestUri.Scheme;

            if (IsTunnelConnect)
            {
                Host = "Tunnel to";
                Url = request.RequestUri.Host + ":" + request.RequestUri.Port;
            }
            else
            {
                Host = request.RequestUri.Host;
                Url = request.RequestUri.AbsolutePath;
            }

            if (!IsTunnelConnect)
            {
                long responseSize = -1;
                if (response != null)
                {
                    if (response.ContentLength != -1)
                    {
                        responseSize = response.ContentLength;
                    }
                    else if (response.IsBodyRead && response.Body != null)
                    {
                        responseSize = response.Body.Length;
                    }
                }

                BodySize = responseSize;
            }

            Process = GetProcessDescription(WebSession.ProcessId.Value);
        }

        private string GetProcessDescription(int processId)
        {
            try
            {
                var process = System.Diagnostics.Process.GetProcessById(processId);
                return process.ProcessName + ":" + processId;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
