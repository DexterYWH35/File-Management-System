using FileManagementSystem.Data;
using FileManagementSystem.Models;
using FileManagementSystem.Models.ViewModels;
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
using Microsoft.AspNetCore.Hosting;

[Authorize] // Only logged-in users can upload files
public class FileController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public FileController(ApplicationDbContext context, UserManager<IdentityUser> userManager, IWebHostEnvironment webHostEnvironment)
    {
        _context = context;
        _userManager = userManager;
        _webHostEnvironment = webHostEnvironment;
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

        // Fetch files uploaded by the logged-in user with their labels
        var userFiles = await _context.Files
            .Where(f => f.UserId == user.Id)
            .Include(f => f.FileLabels)
                .ThenInclude(fl => fl.Label)
            .OrderByDescending(f => f.UploadDate)
            .Select(f => new FileViewModel
            {
                Id = f.Id,
                FileName = f.FileName,
                UserId = f.UserId,
                UploadDate = f.UploadDate,
                Labels = f.FileLabels.Select(fl => fl.Label.Name).ToList(),
                FileType = GetFileType(f.FileName),
                FileSize = new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", f.FilePath.TrimStart('/'))).Length
            })
            .ToListAsync();

        // Add user's labels to ViewBag for the filter dropdown
        ViewBag.Labels = await _context.Labels
            .Where(l => l.UserId == user.Id)
            .ToListAsync();

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
    public async Task<IActionResult> Delete(int id, string? returnURl = null)
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

        // ✅ Stay on Admin Dashboard if an Admin deletes a file at admindashboard
        if (User.IsInRole("Admin") && returnURl == "AdminDashboard")
        {
            return RedirectToAction("AdminDashboard", "File");
        }
        return RedirectToAction("Index", "File");
    }

    [HttpGet]
    public async Task<IActionResult> Preview(int id, string? returnUrl = null)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

        var file = isAdmin 
            ? await _context.Files
                .Include(f => f.FileLabels)
                    .ThenInclude(fl => fl.Label)
                .FirstOrDefaultAsync(f => f.Id == id)
            : await _context.Files
                .Include(f => f.FileLabels)
                    .ThenInclude(fl => fl.Label)
                .FirstOrDefaultAsync(f => f.Id == id && f.UserId == user.Id);

        if (file == null)
        {
            return NotFound("File not found or you do not have permission to preview it.");
        }

        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", file.FilePath.TrimStart('/'));

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound("File does not exist on the server.");
        }

        // Get file size
        var fileInfo = new FileInfo(filePath);
        string fileSize = fileInfo.Length < 1024 ? $"{fileInfo.Length} B" :
                         fileInfo.Length < 1024 * 1024 ? $"{fileInfo.Length / 1024:F2} KB" :
                         fileInfo.Length < 1024 * 1024 * 1024 ? $"{fileInfo.Length / (1024 * 1024):F2} MB" :
                         $"{fileInfo.Length / (1024 * 1024 * 1024):F2} GB";

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
        ViewData["FileName"] = file.FileName;
        ViewData["UploadDate"] = file.UploadDate;
        ViewData["FileSize"] = fileSize;
        ViewData["Labels"] = file.FileLabels.Select(fl => fl.Label).ToList();
        ViewData["AvailableLabels"] = await _context.Labels
            .Where(l => l.UserId == user.Id)
            .ToListAsync();
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["IsOwner"] = file.UserId == user.Id;

        return View();
    }

    // Ensure only Admins can access this page
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminDashboard()
    {
        var viewModel = new AdminDashboardViewModel();

        // Get all users with their roles
        var users = await _userManager.Users.ToListAsync();
        foreach (var user in users)
        {
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            var userFiles = await _context.Files
                .Where(f => f.UserId == user.Id)
                .ToListAsync();

            long totalStorage = 0;
            foreach (var file in userFiles)
            {
                try
                {
                    var filePath = Path.Combine(_webHostEnvironment.WebRootPath, file.FilePath.TrimStart('/'));
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Exists)
                    {
                        totalStorage += fileInfo.Length;
                    }
                }
                catch (Exception)
                {
                    // Skip files that can't be accessed
                    continue;
                }
            }

            viewModel.Users.Add(new UserViewModel
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                IsAdmin = isAdmin,
                FileCount = userFiles.Count,
                TotalStorageUsed = totalStorage
            });
        }

        // Get filtered files
        var query = from f in _context.Files
                   join u in _context.Users on f.UserId equals u.Id
                   select new { f, u };

        var files = await query.OrderByDescending(x => x.f.UploadDate).ToListAsync();
        
        viewModel.Files = files.Select(x => {
            long fileSize = 0;
            try
            {
                var filePath = Path.Combine(_webHostEnvironment.WebRootPath, x.f.FilePath.TrimStart('/'));
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Exists)
                {
                    fileSize = fileInfo.Length;
                }
            }
            catch (Exception)
            {
                // Use 0 for files that can't be accessed
            }

            return new FileViewModel
            {
                Id = x.f.Id,
                FileName = x.f.FileName,
                UserId = x.f.UserId,
                UserName = x.u.UserName ?? "Unknown User",
                UploadDate = x.f.UploadDate,
                Labels = x.f.FileLabels.Select(fl => fl.Label.Name).ToList(),
                FileType = GetFileType(x.f.FileName),
                FileSize = fileSize
            };
        }).ToList();

        return View(viewModel);
    }

    [Authorize]
    public async Task<IActionResult> Search(string searchTerm, int? labelId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        // Start with base query
        IQueryable<FileModel> query = _context.Files
            .Where(f => f.UserId == user.Id)
            .Include(f => f.FileLabels)
                .ThenInclude(fl => fl.Label);

        // Apply text search if provided
        if (!string.IsNullOrEmpty(searchTerm))
        {
            string normalizedSearchTerm = searchTerm.ToLower().Replace(" ", "");
            query = query.Where(f => f.FileName.ToLower().Contains(normalizedSearchTerm));
        }

        // Apply label filter if provided
        if (labelId.HasValue)
        {
            query = query.Where(f => f.FileLabels.Any(fl => fl.LabelId == labelId));
        }

        var files = await query
            .OrderByDescending(f => f.UploadDate)
            .Select(f => new FileViewModel
            {
                Id = f.Id,
                FileName = f.FileName,
                UserId = f.UserId,
                UploadDate = f.UploadDate,
                Labels = f.FileLabels.Select(fl => fl.Label.Name).ToList(),
                FileType = GetFileType(f.FileName),
                FileSize = new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", f.FilePath.TrimStart('/'))).Length
            })
            .ToListAsync();

        // Add user's labels to ViewBag for the filter dropdown
        ViewBag.Labels = await _context.Labels
            .Where(l => l.UserId == user.Id)
            .ToListAsync();

        // Add selected label ID to ViewBag to maintain filter state
        ViewBag.SelectedLabelId = labelId;

        return View("Index", files);
    }

    private static string GetFileType(string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLower();
        return extension switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" => "image",
            ".pdf" => "pdf",
            ".mp4" or ".webm" => "video",
            ".mp3" or ".wav" => "audio",
            ".doc" or ".docx" or ".xls" or ".xlsx" => "document",
            _ => "file"
        };
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SearchAllFiles(string searchTerm)
    {
        var viewModel = new AdminDashboardViewModel();

        // Store search term in ViewBag to maintain it in the search input
        ViewBag.SearchTerm = searchTerm;

        // Get all users with their roles
        var users = await _userManager.Users.ToListAsync();
        foreach (var user in users)
        {
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            var userFiles = await _context.Files
                .Where(f => f.UserId == user.Id)
                .ToListAsync();

            long totalStorage = 0;
            foreach (var file in userFiles)
            {
                try
                {
                    var filePath = Path.Combine(_webHostEnvironment.WebRootPath, file.FilePath.TrimStart('/'));
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Exists)
                    {
                        totalStorage += fileInfo.Length;
                    }
                }
                catch (Exception)
                {
                    // Skip files that can't be accessed
                    continue;
                }
            }

            viewModel.Users.Add(new UserViewModel
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                IsAdmin = isAdmin,
                FileCount = userFiles.Count,
                TotalStorageUsed = totalStorage
            });
        }

        // Get filtered files
        var query = from f in _context.Files
                   join u in _context.Users on f.UserId equals u.Id
                   select new { f, u };

        if (!string.IsNullOrEmpty(searchTerm))
        {
            string lowerSearchTerm = searchTerm.ToLower();
            query = query.Where(x => x.f.FileName.ToLower().Contains(lowerSearchTerm) || 
                                   x.u.UserName.ToLower().Contains(lowerSearchTerm));
        }

        var files = await query.OrderByDescending(x => x.f.UploadDate).ToListAsync();
        
        viewModel.Files = files.Select(x => {
            long fileSize = 0;
            try
            {
                var filePath = Path.Combine(_webHostEnvironment.WebRootPath, x.f.FilePath.TrimStart('/'));
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Exists)
                {
                    fileSize = fileInfo.Length;
                }
            }
            catch (Exception)
            {
                // Use 0 for files that can't be accessed
            }

            return new FileViewModel
            {
                Id = x.f.Id,
                FileName = x.f.FileName,
                UserId = x.f.UserId,
                UserName = x.u.UserName ?? "Unknown User",
                UploadDate = x.f.UploadDate,
                Labels = x.f.FileLabels.Select(fl => fl.Label.Name).ToList(),
                FileType = GetFileType(x.f.FileName),
                FileSize = fileSize
            };
        }).ToList();

        return View("AdminDashboard", viewModel);
    }
}

