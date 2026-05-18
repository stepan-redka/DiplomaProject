using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Diploma.Web.Models.Account;
using Diploma.Application.Interfaces.Chat;
using Microsoft.AspNetCore.Authorization;

namespace Diploma.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly IRagService _ragService;
    private readonly IChatHistoryService _chatHistoryService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<IdentityUser> userManager, 
        SignInManager<IdentityUser> signInManager,
        IRagService ragService,
        IChatHistoryService chatHistoryService,
        ILogger<AccountController> _logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _ragService = ragService;
        _chatHistoryService = chatHistoryService;
        this._logger = _logger;
    }

    [HttpGet]
    public IActionResult Register() => View(); // This shows the Register.cshtml form

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = new IdentityUser { UserName = model.Email, Email = model.Email };
            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        _logger.LogWarning("User {UserId} initiated account deletion.", user.Id);

        try
        {
            // 1. Purge RAG Data (Documents, Chunks, Vector DB)
            // The service already handles global query filters for the current user
            await _ragService.ClearCollectionAsync();

            // 2. Delete Chat History
            var sessions = await _chatHistoryService.GetUserSessionsAsync();
            foreach (var session in sessions)
            {
                await _chatHistoryService.DeleteSessionAsync(session.Id);
            }

            // 3. Delete Identity User
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                _logger.LogError("Failed to delete user identity for {UserId}", user.Id);
                return BadRequest("Failed to delete account security profile.");
            }

            // 4. Sign out
            await _signInManager.SignOutAsync();

            _logger.LogInformation("Successfully purged all data for user {UserId}.", user.Id);
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during account deletion for {UserId}", user.Id);
            return StatusCode(500, "A critical error occurred while purging your research profile.");
        }
    }

    [HttpGet]
    public IActionResult Login() => View();
 // This shows the Login.cshtml form

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (ModelState.IsValid)
        {        
            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, isPersistent: false, lockoutOnFailure: false);
            
            if (result.Succeeded)
            {
                return RedirectToAction("Index", "Home");
            }

            // SignInResult does not expose Errors. Add a generic model error for failed sign-in.
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        }
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }


}