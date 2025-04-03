using FileManagementSystem.Models.ViewModels;
using FileManagementSystem.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authorization;

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
                Console.WriteLine("Login failed. Invalid userName or password.");
                ModelState.AddModelError("", "Invalid userName or password.");
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

            //check if the username is already taken
            var existingUser = await _userManager.FindByNameAsync(model.FullName);
            if (existingUser != null)
            {
                Console.WriteLine($"Username {model.FullName} is already taken.");
                ModelState.AddModelError("", "Username is already taken.");
                ViewData["HideRegisterButton"] = true;
                return View(model);
            }

            // Check if email already exists
            var existingEmail = await _userManager.FindByEmailAsync(model.Email);
            if (existingEmail != null)
            {
                Console.WriteLine($"Email {model.Email} is already registered.");
                ModelState.AddModelError("", "This email is already registered. Please use another.");
                ViewData["HideRegisterButton"] = true;
                return View(model);
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
            
        [HttpGet]
        [Authorize]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (result.Succeeded)
            {
                ViewData["Message"] = "Your password has been changed successfully.";
                return View();
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }
    }
}
