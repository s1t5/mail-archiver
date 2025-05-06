using MailArchiver.Models;
using MailArchiver.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace MailArchiver.Controllers
{
    public class HomeController : Controller
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(IEmailService emailService, ILogger<HomeController> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var model = await _emailService.GetDashboardStatisticsAsync();
            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}