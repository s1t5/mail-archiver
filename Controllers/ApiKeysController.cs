using MailArchiver.Models.ViewModels;
using MailArchiver.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace MailArchiver.Controllers
{
    public class ApiKeysController : Controller
    {
        private readonly IApiKeyService _apiKeyService;
        private readonly IAuthenticationService _authService;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public ApiKeysController(
            IApiKeyService apiKeyService,
            IAuthenticationService authService,
            IStringLocalizer<SharedResource> localizer)
        {
            _apiKeyService = apiKeyService;
            _authService = authService;
            _localizer = localizer;
        }

        public async Task<IActionResult> Index()
        {
            var uid = _authService.GetCurrentUserId(HttpContext);
            if (!uid.HasValue)
            {
                return Unauthorized();
            }

            var isAdmin = _authService.IsCurrentUserAdmin(HttpContext);
            var keys = isAdmin
                ? (await _apiKeyService.GetAllKeysAsync()).Select(k => ApiKeyViewModel.FromEntity(k, includeOwner: true)).ToList()
                : (await _apiKeyService.GetKeysForUserAsync(uid.Value)).Select(k => ApiKeyViewModel.FromEntity(k, includeOwner: false)).ToList();

            ViewBag.IsAdmin = isAdmin;
            return View(keys);
        }

        public IActionResult Create()
        {
            return View(new CreateApiKeyViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateApiKeyViewModel model)
        {
            var uid = _authService.GetCurrentUserId(HttpContext);
            if (!uid.HasValue)
            {
                return Unauthorized();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var (_, plaintext) = await _apiKeyService.CreateAsync(uid.Value, model.Name, model.ExpiresAt);
            TempData["NewApiKey"] = plaintext;
            TempData["NewApiKeyName"] = model.Name;
            TempData["SuccessMessage"] = _localizer["ApiKeyCreatedSuccess"].Value;

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Revoke(int id)
        {
            var uid = _authService.GetCurrentUserId(HttpContext);
            if (!uid.HasValue)
            {
                return Unauthorized();
            }

            var isAdmin = _authService.IsCurrentUserAdmin(HttpContext);
            var ok = await _apiKeyService.RevokeAsync(id, uid.Value, isAdmin);
            TempData[ok ? "SuccessMessage" : "ErrorMessage"] = ok
                ? _localizer["ApiKeyRevokedSuccess"].Value
                : _localizer["ApiKeyRevokeFailed"].Value;

            return RedirectToAction(nameof(Index));
        }
    }
}
