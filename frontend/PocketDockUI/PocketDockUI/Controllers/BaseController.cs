using Microsoft.AspNetCore.Mvc;
using PocketDockUI.Extensions;

namespace PocketDockUI.Controllers;

public class BaseController : Controller
{
        private readonly ILogger _logger;

        public BaseController(ILogger logger) : base()
        {
                _logger = logger;
        }
        
        protected ActionResult GoHome(string key, string message)
        {
                HttpContext.Session.AddBanner(message);
                _logger.LogWarning($"Server creation error: {key}: {message}");
                return RedirectToAction("Index", "Home", new RouteValueDictionary { { key,  1 } });
        }
}