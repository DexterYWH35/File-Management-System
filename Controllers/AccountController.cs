using FileManagementSystem.Models.ViewModels;
using FileManagementSystem.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;

namespace FileManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public AccountController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

//GET
        public IActionResult Login()
        {
            ViewData["HideLoginButton"] = "true"; // Hide Register button in navbar
            return View();
        }

//POST
       [HttpPost]
public async Task<IActionResult> Login(LoginViewModel model)
{
    if (!ModelState.IsValid)
    {
        Console.WriteLine("Model is not valid.");

        // Print validation errors
        foreach (var error in ModelState)
        {
            foreach (var subError in error.Value.Errors)
            {
                Console.WriteLine($"Validation Error: {subError.ErrorMessage}");
            }
        }
         ViewData["HideLoginButton"] = "true"; 
        return View();
    }
    Console.WriteLine("UserName: "+model.UserName);
    var result = await _signInManager.PasswordSignInAsync(model.UserName, model.Password, model.RememberMe, false);

    if (result.Succeeded)
    {
        Console.WriteLine($"User {model.UserName} logged in successfully.");
        return RedirectToAction("Index", "Home");
    }
    else
    {
        Console.WriteLine("Login failed. Invalid email or password.");
        ModelState.AddModelError("", "Invalid email or password.");
    }
     ViewData["HideLoginButton"] = "true"; 
    return View();
}



//GET
public IActionResult Register()
{
ViewData["HideRegisterButton"] = "true"; // HideRegisterButton
return View();
}

//POST
 [HttpPost]
public async Task<IActionResult> Register(RegisterViewModel model)
{
    if (!ModelState.IsValid)
    {
        Console.WriteLine("Model is not valid.");
        
        // Print validation errors
        foreach (var error in ModelState)
        {
            foreach (var subError in error.Value.Errors)
            {
                Console.WriteLine($"Validation Error: {subError.ErrorMessage}");
            }
        }
         ViewData["HideRegisterButton"] = true;
        return View();
    }

    var user = new IdentityUser { UserName = model.FullName, Email = model.Email };
    var result = await _userManager.CreateAsync(user, model.Password);

    if (result.Succeeded)
    {
        Console.WriteLine($"User {user.Email} registered successfully.");
        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Login", "Account");
    }

    foreach (var error in result.Errors)
    {
        Console.WriteLine($"Error: {error.Description}");
        ModelState.AddModelError("", error.Description);
    }

     ViewData["HideRegisterButton"] = true;
    return View(model);
}

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            HttpContext.Response.Cookies.Delete(".AspNetCore.Identity.Application"); 

              Console.WriteLine("User logged out successfully.");


            return RedirectToAction("Login", "Account");
        }
            
    }
}
