
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace TaskFlow.Areas.Identity.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IUserStore<IdentityUser> _userStore;
    private readonly IUserEmailStore<IdentityUser> _emailStore;
    private readonly ILogger<RegisterModel> _logger;
    private readonly IEmailSender _emailSender;

    public RegisterModel(
        UserManager<IdentityUser> userManager,
        IUserStore<IdentityUser> userStore,
        SignInManager<IdentityUser> signInManager,
        ILogger<RegisterModel> logger,
        IEmailSender emailSender)
    {
        _userManager = userManager;
        _userStore = userStore;
        _emailStore = GetEmailStore();
        _signInManager = signInManager;
        _logger = logger;
        _emailSender = emailSender;
    }

    [BindProperty]
    public InputModel Input { get; set; } = default!;

    public string? ReturnUrl { get; set; }

    public IList<AuthenticationScheme>? ExternalLogins { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = default!;

        [Required]
        [StringLength(
            100,
            ErrorMessage =
                "The {0} must be at least {2} and at max {1} characters long.",
            MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = default!;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare(
            "Password",
            ErrorMessage =
                "The password and confirmation password do not match.")]
        public string? ConfirmPassword { get; set; }
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;

        ExternalLogins =
            (await _signInManager.GetExternalAuthenticationSchemesAsync())
            .ToList();
    }

    public async Task<IActionResult> OnPostAsync(
        string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        ReturnUrl = returnUrl;

        ExternalLogins =
            (await _signInManager.GetExternalAuthenticationSchemesAsync())
            .ToList();

        if (!ModelState.IsValid)
            return Page();

        var user = CreateUser();

        await _userStore.SetUserNameAsync(
            user, Input.Email, CancellationToken.None);

        await _emailStore.SetEmailAsync(
            user, Input.Email, CancellationToken.None);

        var result = await _userManager.CreateAsync(
            user, Input.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    string.Empty, error.Description);
            }

            return Page();
        }

        var roleResult = await _userManager.AddToRoleAsync(
            user, "TeamMember");

        if (!roleResult.Succeeded)
        {
            _logger.LogError(
                "Could not assign TeamMember role to {Email}.",
                Input.Email);

            // Do not leave a newly registered account without its role.
            var deleteResult = await _userManager.DeleteAsync(user);

            if (!deleteResult.Succeeded)
            {
                _logger.LogError(
                    "Could not remove user {Email} after role assignment failed.",
                    Input.Email);
            }

            foreach (var error in roleResult.Errors)
            {
                ModelState.AddModelError(
                    string.Empty, error.Description);
            }

            return Page();
        }

        _logger.LogInformation(
            "User created an account and was assigned TeamMember.");

        var userId = await _userManager.GetUserIdAsync(user);

        var code =
            await _userManager.GenerateEmailConfirmationTokenAsync(user);

        code = WebEncoders.Base64UrlEncode(
            Encoding.UTF8.GetBytes(code));

        var callbackUrl = Url.Page(
            "/Account/ConfirmEmail",
            pageHandler: null,
            values: new
            {
                area = "Identity",
                userId,
                code,
                returnUrl
            },
            protocol: Request.Scheme)!;

        await _emailSender.SendEmailAsync(
            Input.Email,
            "Confirm your email",
            $"Please confirm your account by " +
            $"<a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>" +
            "clicking here</a>.");

        if (_userManager.Options.SignIn.RequireConfirmedAccount)
        {
            return RedirectToPage(
                "RegisterConfirmation",
                new { email = Input.Email, returnUrl });
        }

        await _signInManager.SignInAsync(
            user, isPersistent: false);

        return LocalRedirect(returnUrl);
    }

    private IdentityUser CreateUser()
    {
        try
        {
            return Activator.CreateInstance<IdentityUser>();
        }
        catch
        {
            throw new InvalidOperationException(
                "Unable to create an IdentityUser instance.");
        }
    }

    private IUserEmailStore<IdentityUser> GetEmailStore()
    {
        if (!_userManager.SupportsUserEmail)
        {
            throw new NotSupportedException(
                "The Identity user store must support email.");
        }

        return (IUserEmailStore<IdentityUser>)_userStore;
    }
}
