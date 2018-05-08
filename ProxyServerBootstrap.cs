using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Models;

namespace Server
{
    public static class ProxyServerBootstrap
    {
        private static string WebServerUrl;
        private static ProxyServerSetting setting;
        private static ProxyServer proxyServer;
        public static int ClientConnectionCount;
        public static int ServerConnectionCount;
        public static List<SessionListItem> Sessions { get; } = new List<SessionListItem>();
        public static int lastSessionNumber { get; private set; }
        private static readonly Dictionary<HttpWebClient, SessionListItem> sessionDictionary =
            new Dictionary<HttpWebClient, SessionListItem>();

        public static void ProxyServer(this IServiceCollection services)
        {
            var configuration = services.BuildServiceProvider().GetService<IConfiguration>();
            WebServerUrl = configuration.GetValue<string>("URLS");
            setting = new ProxyServerSetting()
            {
                Port = configuration.GetValue<int>("Port"),
                SetAsSystemProxy = configuration.GetValue<bool>("SetAsSystemProxy")
            };
            proxyServer = new ProxyServer();
            //proxyServer.CertificateManager.CertificateEngine = CertificateEngine.DefaultWindows;

            ////Set a password for the .pfx file
            //proxyServer.CertificateManager.PfxPassword = "PfxPassword";

            ////Set Name(path) of the Root certificate file
            //proxyServer.CertificateManager.PfxFilePath = @"C:\NameFolder\rootCert.pfx";

            ////do you want Replace an existing Root certificate file(.pfx) if password is incorrect(RootCertificate=null)?  yes====>true
            //proxyServer.CertificateManager.OverwritePfxFile = true;

            ////save all fake certificates in folder "crts"(will be created in proxy dll directory)
            ////if create new Root certificate file(.pfx) ====> delete folder "crts"
            //proxyServer.CertificateManager.SaveFakeCertificates = true;

            proxyServer.ForwardToUpstreamGateway = true;

            ////if you need Load or Create Certificate now. ////// "true" if you need Enable===> Trust the RootCertificate used by this proxy server
            //proxyServer.CertificateManager.EnsureRootCertificate(true);

            ////or load directly certificate(As Administrator if need this)
            ////and At the same time chose path and password
            ////if password is incorrect and (overwriteRootCert=true)(RootCertificate=null) ====> replace an existing .pfx file
            ////note : load now (if existed)
            //proxyServer.CertificateManager.LoadRootCertificate(@"C:\NameFolder\rootCert.pfx", "PfxPassword");
            var explicitEndPoint = new ExplicitProxyEndPoint(IPAddress.Any, setting.Port, true);

            proxyServer.AddEndPoint(explicitEndPoint);
            //proxyServer.UpStreamHttpProxy = new ExternalProxy
            //{
            //    HostName = "158.69.115.45",
            //    Port = 3128,
            //    UserName = "Titanium",
            //    Password = "Titanium",
            //};

            proxyServer.BeforeRequest += ProxyServer_BeforeRequest;
            proxyServer.BeforeResponse += ProxyServer_BeforeResponse;
            proxyServer.AfterResponse += ProxyServer_AfterResponse;
            explicitEndPoint.BeforeTunnelConnectRequest += ProxyServer_BeforeTunnelConnectRequest;
            explicitEndPoint.BeforeTunnelConnectResponse += ProxyServer_BeforeTunnelConnectResponse;
            proxyServer.ClientConnectionCountChanged += delegate
            {
                ClientConnectionCount = proxyServer.ClientConnectionCount;
            };
            proxyServer.ServerConnectionCountChanged += delegate
            {
                ServerConnectionCount = proxyServer.ServerConnectionCount;
            };
            proxyServer.Start();
            if (setting.SetAsSystemProxy)
                proxyServer.SetAsSystemProxy(explicitEndPoint, ProxyProtocolType.AllHttp);
        }

        private static Task ProxyServer_BeforeTunnelConnectResponse(object sender, TunnelConnectSessionEventArgs e)
        {
            string hostname = e.WebSession.Request.RequestUri.Host;
            if (hostname.EndsWith("webex.com"))
            {
                e.DecryptSsl = false;
            }

            AddSession(e);
            return Task.CompletedTask;
        }

        private static Task ProxyServer_BeforeTunnelConnectRequest(object sender, TunnelConnectSessionEventArgs e)
        {
            if (sessionDictionary.TryGetValue(e.WebSession, out var item))
            {
                item.Update();
            }
            return Task.CompletedTask;
        }

        private static Task ProxyServer_AfterResponse(object sender, SessionEventArgs e)
        {
            if (sessionDictionary.TryGetValue(e.WebSession, out var item))
            {
                item.Exception = e.Exception;
            }
            return Task.CompletedTask;
        }

        private static async Task ProxyServer_BeforeResponse(object sender, SessionEventArgs e)
        {
            SessionListItem item = null;
            if (sessionDictionary.TryGetValue(e.WebSession, out item))
            {
                item.Update();
            }

            if (item != null)
            {
                if (e.WebSession.Response.HasBody)
                {
                    e.WebSession.Response.KeepBody = true;
                    await e.GetResponseBody();
                    item.Update();
                }
            }
        }

        private static async Task ProxyServer_BeforeRequest(object sender, SessionEventArgs e)
        {
            AddSession(e);

            if (e.WebSession.Request.HasBody)
            {
                e.WebSession.Request.KeepBody = true;
                await e.GetRequestBody();
            }
        }

        private static SessionListItem AddSession(SessionEventArgsBase e)
        {
            if (e.WebSession.Request.Url.StartsWith(WebServerUrl))
                return null;
            if (Sessions.Count > 500)
            {
                Sessions.Clear();
                sessionDictionary.Clear();
            }
            var item = CreateSessionListItem(e);
            Sessions.Add(item);
            if (!sessionDictionary.ContainsKey(e.WebSession))
                sessionDictionary.Add(e.WebSession, item);
            return item;
        }

        private static SessionListItem CreateSessionListItem(SessionEventArgsBase e)
        {
            lastSessionNumber++;
            bool isTunnelConnect = e is TunnelConnectSessionEventArgs;
            var item = new SessionListItem
            {
                Number = lastSessionNumber,
                WebSession = e.WebSession,
                IsTunnelConnect = isTunnelConnect
            };

            if (isTunnelConnect || e.WebSession.Request.UpgradeToWebSocket)
            {
                e.DataReceived += (sender, args) =>
                {
                    var session = (SessionEventArgs)sender;
                    if (sessionDictionary.TryGetValue(session.WebSession, out var li))
                    {
                        li.ReceivedDataCount += args.Count;
                    }
                };

                e.DataSent += (sender, args) =>
                {
                    var session = (SessionEventArgs)sender;
                    if (sessionDictionary.TryGetValue(session.WebSession, out var li))
                    {
                        li.SentDataCount += args.Count;
                    }
                };
            }

            item.Update();
            return item;
        }
    }
}
