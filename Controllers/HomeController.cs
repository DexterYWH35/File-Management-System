using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FileManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FileManagementSystem.Data;

namespace FileManagementSystem.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
    }
    
    [Authorize]
    public async Task<IActionResult> Index()
    {
        Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";
        
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            var recentFiles = await _context.Files
                .Where(f => f.UserId == user.Id)
                .OrderByDescending(f => f.UploadDate)
                .Take(5)
                .ToListAsync();
            
            ViewBag.RecentFiles = recentFiles;
        }
        
        return View();
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

    [Authorize(Roles = "Admin")]
    public IActionResult AdminDashboard()
    {
    return View();
    }
}
