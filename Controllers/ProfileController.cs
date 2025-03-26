using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using FileManagementSystem.Models.ViewModels;

namespace FileManagementSystem.Controllers
{
    [Authorize] // Ensure only logged-in users can access the profile page
    public class ProfileController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public ProfileController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new ProfileViewModel
            {
                Email = user.Email,
                FullName = user.UserName // Modify this if you store full name separately
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            user.UserName = model.FullName;
            user.Email = model.Email;

            Console.WriteLine("Updated UserName is: "+user.UserName);
            Console.WriteLine("Updated Email is: "+user.Email);

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                ViewData["Message"] = "Profile updated successfully!";
            }
            else
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }

            return View("Index", model);
        }
    }
}
