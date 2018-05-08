using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Server.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult SessionList(int page = 1, string url = null, string host = null, string process = null)
        {
            var result = !string.IsNullOrEmpty(url) || !string.IsNullOrEmpty(host) || !string.IsNullOrEmpty(url)
                ? ProxyServerBootstrap.Sessions.Where(s => (string.IsNullOrEmpty(url) || s.Url.Contains(url)) && (string.IsNullOrEmpty(host) || s.Host.Contains(host)) && (string.IsNullOrEmpty(process) || s.Process.Contains(process))) : ProxyServerBootstrap.Sessions;
            var pageSize = 45;
            return Json(result.Skip((page - 1) * pageSize).Take(pageSize).Select(s => new { s.Url, s.Process, s.Protocol, s.Number, s.Host, s.StatusCode, s.ReceivedDataCount, s.SentDataCount }));
        }

        public IActionResult Info(int id)
        {
            var result = ProxyServerBootstrap.Sessions.FirstOrDefault(s => s.Number == id);
            if (result == null)
                return Json("");
            return Json(new { Url = result.WebSession.Request.RequestUri.ToString(), RequestHeader = result.WebSession.Request.HeaderText, RequestBody = result.WebSession.Request.HasBody ? result.WebSession.Request.BodyString : null, ResponseHeaderText = result.WebSession.Response.HeaderText, ResponseBody = result.WebSession.Response.HasBody ? result.WebSession.Response.BodyString : null });
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Exit()
        {
            Task.Run(async () =>
            {
                await Program.Host.StopAsync();
            });
            return Content("Exting ...");
        }
    }
}
