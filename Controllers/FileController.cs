using FileManagementSystem.Data;
using FileManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.Authorization;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

[Authorize] // Only logged-in users can upload files
public class FileController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public FileController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public IActionResult Upload()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            ViewData["Message"] = "Please select a file to upload.";
            return View();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        // Save the file to "wwwroot/uploads" folder
        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var filePath = Path.Combine(uploadsFolder, file.FileName);
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Save file details to database
        var fileModel = new FileModel
        {
            FileName = file.FileName,
            FilePath = "/uploads/" + file.FileName,
            UserId = user.Id,
            UploadDate = DateTime.UtcNow
        };

        _context.Files.Add(fileModel);
        await _context.SaveChangesAsync();

        ViewData["Message"] = "File uploaded successfully!";
        return View();
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        // Fetch files uploaded by the logged-in user
        var userFiles = _context.Files
            .Where(f => f.UserId == user.Id)
            .OrderByDescending(f => f.UploadDate)
            .ToList();

        return View(userFiles);
    }

    [HttpGet]
public async Task<IActionResult> Download(int id)
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null)
    {
        return RedirectToAction("Login", "Account");
    }

    var file = await _context.Files.FirstOrDefaultAsync(f => f.Id == id);
    if (file == null)
    {      
        return NotFound("File not found.");
    }

    // Allow Admin to access all files
    if (file.UserId != user.Id && !User.IsInRole("Admin"))
    {
        return Forbid(); // Return 403 Forbidden if user is not the owner or Admin
    }
    if (file == null)
    {
        return NotFound("File not found or you do not have permission to download it.");
    }

    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", file.FilePath.TrimStart('/'));

    if (!System.IO.File.Exists(filePath))
    {
        return NotFound("File does not exist on the server.");
    }

    var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
    return File(fileBytes, "application/octet-stream", file.FileName);
}

[HttpPost]
public async Task<IActionResult> Delete(int id)
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null)
    {
        return RedirectToAction("Login", "Account");
    }

    var file = await _context.Files.FirstOrDefaultAsync(f => f.Id == id);
if (file == null)
{
    return NotFound("File not found.");
}

// Allow Admin to delete any file
if (file.UserId != user.Id && !User.IsInRole("Admin"))
{
    return Forbid(); // Return 403 Forbidden if user is not the owner or Admin
}

// Proceed with deletion
var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", file.FilePath.TrimStart('/'));
if (System.IO.File.Exists(filePath))
{
    System.IO.File.Delete(filePath);
}

_context.Files.Remove(file);
await _context.SaveChangesAsync();
return RedirectToAction("Index");


    if (file == null)
    {
        return NotFound("File not found or you do not have permission to delete it.");
    }

    filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", file.FilePath.TrimStart('/'));

    if (System.IO.File.Exists(filePath))
    {
        System.IO.File.Delete(filePath);
    }

    _context.Files.Remove(file); 
    await _context.SaveChangesAsync();

    return RedirectToAction("Index"); 
}

[HttpGet]
public async Task<IActionResult> Preview(int id)
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null)
    {
        return RedirectToAction("Login", "Account");
    }

     bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

    var file = isAdmin 
        ? _context.Files.FirstOrDefault(f => f.Id == id) 
        : _context.Files.FirstOrDefault(f => f.Id == id && f.UserId == user.Id);

    if (file == null)
    {
        return NotFound("File not found or you do not have permission to preview it.");
    }

    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", file.FilePath.TrimStart('/'));

    if (!System.IO.File.Exists(filePath))
    {
        return NotFound("File does not exist on the server.");
    }

    // Determine file type
    string extension = Path.GetExtension(file.FileName).ToLower();
    string contentType = extension switch
    {
        ".jpg" or ".jpeg" or ".png" or ".gif" => "image",
        ".pdf" => "pdf",
        ".mp4" or ".webm" => "video",
        ".mp3" or ".wav" => "audio",
        ".doc" or ".docx" or ".xls" or ".xlsx" => "document",
        _ => "unsupported"
    };

    ViewData["FilePath"] = file.FilePath;
    ViewData["FileType"] = contentType;
    ViewData["FileId"] = file.Id;
    return View();
}

// Ensure only Admins can access this page
[Authorize(Roles = "Admin")]
public async Task<IActionResult> AdminDashboard()
{
 var files = await (from f in _context.Files
                       join u in _context.Users on f.UserId equals u.Id
                       orderby f.UploadDate descending
                       select new FileViewModel
                       {
                           Id = f.Id,
                           FileName = f.FileName,
                           UserId = f.UserId,
                           UserName = u.UserName,
                           UploadDate = f.UploadDate
                       }).ToListAsync();

    return View(files);
}

[Authorize]
public async Task<IActionResult> Search(string searchTerm)
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null)
    {
        return RedirectToAction("Login", "Account");
    }
    string normalizedSearchTerm = searchTerm?.ToLower().Replace(" ", "") ?? "";

    var files = _context.Files.Where(f => f.UserId == user.Id);

    if (!string.IsNullOrEmpty(searchTerm))
    {
        files = files.Where(f => f.FileName.Contains(normalizedSearchTerm));
    }

    var sortedFiles = files.OrderByDescending(f => f.UploadDate).ToList();

    return View("Index", sortedFiles);
}

[Authorize(Roles = "Admin")]
    public async Task<IActionResult> SearchAllFiles(string searchTerm)
    {
    var files = _context.Files.AsQueryable();

    string normalizedSearchTerm = searchTerm?.ToLower().Replace(" ", "") ?? "";

    if (!string.IsNullOrEmpty(searchTerm))
    {
        files = files.Where(f => f.FileName.Contains(normalizedSearchTerm));
    }

    var sortedFiles = await files.OrderByDescending(f => f.UploadDate).ToListAsync();

    return View("AdminDashboard", sortedFiles);
    }


}

