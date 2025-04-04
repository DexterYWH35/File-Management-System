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

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("User ID is required");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound("User not found");
            }

            // Don't allow deleting the admin user
            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return BadRequest("Cannot delete admin user");
            }

            // Delete user's files first
            var userFiles = _context.Files.Where(f => f.UserId == userId);
            foreach (var file in userFiles)
            {
                // Delete physical file
                var filePath = Path.Combine(_webHostEnvironment.WebRootPath, file.FilePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            // Remove files from database
            _context.Files.RemoveRange(userFiles);
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
    }
}
