using FileManagementSystem.Models.ViewModels;
using FileManagementSystem.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authorization;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace FileManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AccountController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _webHostEnvironment = webHostEnvironment;
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
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    var result = await _signInManager.PasswordSignInAsync(user.UserName, model.Password, model.RememberMe, false);
                    if (result.Succeeded)
                    {
                        return RedirectToAction("Index", "Home");
                    }
                }
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            }
            return View(model);
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
            if (ModelState.IsValid)
            {
                var user = new IdentityUser
                {
                    UserName = model.UserName,
                    Email = model.Email
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await _signInManager.SignInAsync(user, isPersistent: false);
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

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["ErrorMessage"] = "User ID is required.";
                return RedirectToAction("AdminDashboard", "File");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("AdminDashboard", "File");
            }

            // Check if user is admin
            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                TempData["ErrorMessage"] = "Cannot delete admin users.";
                return RedirectToAction("AdminDashboard", "File");
            }

            // Delete user's files
            var userFiles = await _context.Files.Where(f => f.UserId == user.Id).ToListAsync();
            foreach (var file in userFiles)
            {
                var filePath = Path.Combine(_webHostEnvironment.WebRootPath, file.FilePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    try
                    {
                        System.IO.File.Delete(filePath);
                    }
                    catch (Exception ex)
                    {
                        // Log the error but continue with user deletion
                        Console.WriteLine($"Error deleting file {filePath}: {ex.Message}");
                    }
                }
                _context.Files.Remove(file);
            }
            await _context.SaveChangesAsync();

            // Delete the user
            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = $"User {user.UserName} has been deleted successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = $"Error deleting user: {string.Join(", ", result.Errors.Select(e => e.Description))}";
            }

            return RedirectToAction("AdminDashboard", "File");
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            var userFiles = await _context.Files
                .Where(f => f.UserId == user.Id)
                .ToListAsync();

            var totalStorageUsed = 0L;
            foreach (var file in userFiles)
            {
                try
                {
                    var filePath = Path.Combine(_webHostEnvironment.WebRootPath, file.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        var fileInfo = new FileInfo(filePath);
                        totalStorageUsed += fileInfo.Length;
                    }
                }
                catch
                {
                    // Skip files that can't be accessed
                    continue;
                }
            }

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "User";

            var viewModel = new ProfileViewModel
            {
                UserName = user.UserName,
                Email = user.Email,
                Role = role,
                TotalFiles = userFiles.Count,
                StorageUsed = totalStorageUsed
            };

            return View(viewModel);
        }

        [HttpGet]
        [Authorize]
        public IActionResult ChangeEmail()
        {
            var user = _userManager.GetUserAsync(User).Result;
            var model = new ChangeEmailViewModel
            {
                CurrentEmail = user.Email
            };
            return View(model);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ChangeEmail(ChangeEmailViewModel model)
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

            // Verify current email matches
            if (user.Email != model.CurrentEmail)
            {
                ModelState.AddModelError(string.Empty, "Current email does not match your account email.");
                return View(model);
            }

            // Verify password
            var isPasswordValid = await _userManager.CheckPasswordAsync(user, model.Password);
            if (!isPasswordValid)
            {
                ModelState.AddModelError(string.Empty, "Password is incorrect.");
                return View(model);
            }

            // Check if new email is already taken
            var existingUser = await _userManager.FindByEmailAsync(model.NewEmail);
            if (existingUser != null)
            {
                ModelState.AddModelError(string.Empty, "This email is already in use by another account.");
                return View(model);
            }

            // Update email
            var token = await _userManager.GenerateChangeEmailTokenAsync(user, model.NewEmail);
            var result = await _userManager.ChangeEmailAsync(user, model.NewEmail, token);

            if (result.Succeeded)
            {
                // Update username to match new email
                user.UserName = model.NewEmail;
                await _userManager.UpdateAsync(user);

                TempData["SuccessMessage"] = "Your email has been changed successfully.";
                return RedirectToAction("Profile");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }
    }
}
