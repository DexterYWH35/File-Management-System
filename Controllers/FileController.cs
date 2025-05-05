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
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Drawing;

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
    public async Task<IActionResult> Upload(int? folderId = null)
    {
        ViewBag.FolderId = folderId;
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            var files = await _context.Files
                .Where(f => f.UserId == user.Id && f.FolderId == folderId)
                .OrderByDescending(f => f.UploadDate)
                .Take(10)
                .ToListAsync();
            ViewBag.RecentFiles = files;
        }
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile[] files, int? folderId = null)
    {
        if (files == null || files.Length == 0 || files.All(f => f == null || f.Length == 0))
        {
            ViewData["Message"] = "Please select at least one file to upload.";
            ViewBag.FolderId = folderId;
            return View();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        int uploadedCount = 0;
        foreach (var file in files)
        {
            if (file == null || file.Length == 0) continue;
            var filePath = Path.Combine(uploadsFolder, file.FileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            var fileModel = new FileModel
            {
                FileName = file.FileName,
                FilePath = "/uploads/" + file.FileName,
                UserId = user.Id,
                UploadDate = DateTime.UtcNow,
                FolderId = folderId
            };
            _context.Files.Add(fileModel);
            uploadedCount++;
        }
        await _context.SaveChangesAsync();

        ViewData["Message"] = $"{uploadedCount} file(s) uploaded successfully!";
        ViewBag.FolderId = folderId;
        return View();
    }

    private async Task<List<FolderModel>> GetFolderBreadcrumbs(int? folderId)
    {
        var breadcrumbs = new List<FolderModel>();
        while (folderId != null)
        {
            var folder = await _context.Folders.FindAsync(folderId);
            if (folder == null) break;
            breadcrumbs.Insert(0, folder);
            folderId = folder.ParentFolderId;
        }
        return breadcrumbs;
    }

    public async Task<IActionResult> Index(int? folderId = null)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        // Get folders in the current folder (owned or shared)
        var ownedFolders = await _context.Folders
            .Where(f => f.UserId == user.Id && f.ParentFolderId == folderId)
            .ToListAsync();
        var folders = ownedFolders.ToList();

        // Get files in the current folder (if user has access)
        var files = await _context.Files
            .Where(f => f.FolderId == folderId && f.UserId == user.Id)
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

        ViewBag.Folders = folders;
        ViewBag.CurrentFolderId = folderId;
        ViewBag.Breadcrumbs = await GetFolderBreadcrumbs(folderId);
        ViewBag.Labels = await _context.Labels.Where(l => l.UserId == user.Id).ToListAsync();
        return View(files);
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction("Login", "Account");

        var file = await _context.Files.FirstOrDefaultAsync(f => f.Id == id);
        if (file == null)
            return NotFound("File not found.");

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
            return RedirectToAction("Login", "Account");

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

    [HttpGet]
    public async Task<IActionResult> Rename(int id)
    {
        var file = await _context.Files
            .Include(f => f.FileLabels)
            .ThenInclude(fl => fl.Label)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (file == null)
        {
            return NotFound();
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (file.UserId != currentUser.Id)
        {
            return Forbid();
        }

        // Construct the full file path
        var fullPath = Path.Combine(_webHostEnvironment.WebRootPath, file.FilePath.TrimStart('/'));
        long fileSize = 0;
        
        // Safely get file size
        if (System.IO.File.Exists(fullPath))
        {
            fileSize = new FileInfo(fullPath).Length;
        }

        var viewModel = new FileViewModel
        {
            Id = file.Id,
            FileName = file.FileName,
            UserId = file.UserId,
            UploadDate = file.UploadDate,
            Labels = file.FileLabels.Select(fl => fl.Label.Name).ToList(),
            FileType = GetFileType(file.FileName),
            FileSize = fileSize
        };

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> Rename(int id, string newFileName)
    {
        var file = await _context.Files.FindAsync(id);
        if (file == null)
        {
            return NotFound();
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (file.UserId != currentUser.Id)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(newFileName))
        {
            ModelState.AddModelError("", "File name cannot be empty");
            return View(new FileViewModel { Id = id, FileName = file.FileName });
        }

        // Get the file extension
        string extension = Path.GetExtension(file.FileName);
        
        // Combine the new name with the original extension
        string newFileNameWithExtension = newFileName + extension;

        // Update the file name in the database
        file.FileName = newFileNameWithExtension;
        _context.Update(file);
        await _context.SaveChangesAsync();

        return RedirectToAction("Preview", new { id = id });
    }

    [HttpGet]
    public async Task<IActionResult> ConvertToPdf(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction("Login", "Account");

        var file = await _context.Files.FirstOrDefaultAsync(f => f.Id == id);
        if (file == null)
            return NotFound("File not found.");

        // Only allow owner or admin
        if (file.UserId != user.Id && !User.IsInRole("Admin"))
            return Forbid();

        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", file.FilePath.TrimStart('/'));
        if (!System.IO.File.Exists(filePath))
            return NotFound("File does not exist on the server.");

        // Generate new file name
        var pdfFileName = Path.GetFileNameWithoutExtension(file.FileName) + ".pdf";
        var pdfFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", pdfFileName);

        var ext = Path.GetExtension(file.FileName).ToLower();
        try
        {
            if (ext == ".txt")
            {
                using (FileStream pdfStream = new FileStream(pdfFilePath, FileMode.Create, FileAccess.Write))
                using (PdfDocument document = new PdfDocument())
                {
                    PdfPage page = document.Pages.Add();
                    string text = System.IO.File.ReadAllText(filePath);
                    PdfFont font = new PdfStandardFont(PdfFontFamily.Helvetica, 12);
                    page.Graphics.DrawString(
                        text,
                        font,
                        PdfBrushes.Black,
                        new Syncfusion.Drawing.RectangleF(0, 0, page.GetClientSize().Width, page.GetClientSize().Height)
                    );
                    document.Save(pdfStream);
                }
            }
            else if (ext == ".jpg" || ext == ".jpeg" || ext == ".png")
            {
                using (FileStream pdfStream = new FileStream(pdfFilePath, FileMode.Create, FileAccess.Write))
                using (PdfDocument document = new PdfDocument())
                {
                    PdfPage page = document.Pages.Add();
                    using (FileStream imageStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    {
                        PdfBitmap pdfBitmap = new PdfBitmap(imageStream);
                        float width = pdfBitmap.PhysicalDimension.Width > page.GetClientSize().Width ? page.GetClientSize().Width : pdfBitmap.PhysicalDimension.Width;
                        float height = pdfBitmap.PhysicalDimension.Height > page.GetClientSize().Height ? page.GetClientSize().Height : pdfBitmap.PhysicalDimension.Height;
                        page.Graphics.DrawImage(pdfBitmap, 0, 0, width, height);
                    }
                    document.Save(pdfStream);
                }
            }
            else
            {
                TempData["ErrorMessage"] = "PDF conversion is only supported for .txt and image files (.jpg, .jpeg, .png) in this demo.";
                return RedirectToAction("Preview", new { id });
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "PDF conversion failed: " + ex.Message;
            return RedirectToAction("Preview", new { id });
        }

        // Save new file to DB
        var newFile = new FileModel
        {
            FileName = pdfFileName,
            FilePath = "/uploads/" + pdfFileName,
            UserId = file.UserId,
            UploadDate = DateTime.UtcNow
        };
        _context.Files.Add(newFile);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "File converted to PDF and saved!";
        return RedirectToAction("Preview", new { id = newFile.Id });
    }

    [HttpPost]
    public async Task<IActionResult> CreateFolder(string folderName, int? parentFolderId = null)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }
        if (string.IsNullOrWhiteSpace(folderName))
        {
            TempData["ErrorMessage"] = "Folder name cannot be empty.";
            return RedirectToAction("Index", new { folderId = parentFolderId });
        }
        var folder = new FolderModel
        {
            Name = folderName,
            UserId = user.Id,
            ParentFolderId = parentFolderId
        };
        _context.Folders.Add(folder);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Folder created successfully!";
        return RedirectToAction("Index", new { folderId = parentFolderId });
    }

    [HttpPost]
    public async Task<IActionResult> RenameFolder(int id, string newName)
    {
        var user = await _userManager.GetUserAsync(User);
        var folder = await _context.Folders.FindAsync(id);
        if (folder == null || folder.UserId != user.Id)
        {
            TempData["ErrorMessage"] = "Folder not found or access denied.";
            return RedirectToAction("Index");
        }
        if (string.IsNullOrWhiteSpace(newName))
        {
            TempData["ErrorMessage"] = "Folder name cannot be empty.";
            return RedirectToAction("Index", new { folderId = folder.ParentFolderId });
        }
        folder.Name = newName;
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Folder renamed successfully!";
        return RedirectToAction("Index", new { folderId = folder.ParentFolderId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteFolder(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var folder = await _context.Folders
            .Include(f => f.SubFolders)
            .Include(f => f.Files)
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == user.Id);
        if (folder == null)
        {
            TempData["ErrorMessage"] = "Folder not found or access denied.";
            return RedirectToAction("Index");
        }
        var parentId = folder.ParentFolderId;
        await DeleteFolderRecursive(folder);
        TempData["SuccessMessage"] = "Folder and all its contents deleted successfully!";
        return RedirectToAction("Index", new { folderId = parentId });
    }

    // Recursively delete all files and subfolders, then the folder itself
    private async Task DeleteFolderRecursive(FolderModel folder)
    {
        // Delete all files in this folder
        foreach (var file in folder.Files.ToList())
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", file.FilePath.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
            _context.Files.Remove(file);
        }
        await _context.SaveChangesAsync();
        // Recursively delete all subfolders
        foreach (var subfolder in folder.SubFolders.ToList())
        {
            var sub = await _context.Folders
                .Include(f => f.SubFolders)
                .Include(f => f.Files)
                .FirstOrDefaultAsync(f => f.Id == subfolder.Id);
            if (sub != null)
            {
                await DeleteFolderRecursive(sub);
            }
        }
        // Remove the folder itself
        _context.Folders.Remove(folder);
        await _context.SaveChangesAsync();
    }

    // Move File - GET (for modal)
    [HttpGet]
    public async Task<IActionResult> MoveFile(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var file = await _context.Files.FindAsync(id);
        if (file == null || file.UserId != user.Id)
            return Forbid();
        var folders = await _context.Folders.Where(f => f.UserId == user.Id).ToListAsync();
        ViewBag.Folders = folders;
        ViewBag.CurrentFolderId = file.FolderId;
        ViewBag.FileId = id;
        return PartialView("_MoveFileModal");
    }

    // Move File - POST
    [HttpPost]
    public async Task<IActionResult> MoveFile(int id, int? targetFolderId)
    {
        var user = await _userManager.GetUserAsync(User);
        var file = await _context.Files.FindAsync(id);
        if (file == null || file.UserId != user.Id)
            return Forbid();
        if (targetFolderId.HasValue)
        {
            var targetFolder = await _context.Folders.FindAsync(targetFolderId);
            if (targetFolder == null || targetFolder.UserId != user.Id)
            {
                TempData["ErrorMessage"] = "Target folder not found or access denied.";
                return RedirectToAction("Index", new { folderId = file.FolderId });
            }
        }
        file.FolderId = targetFolderId;
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "File moved successfully!";
        return RedirectToAction("Index", new { folderId = targetFolderId });
    }

    // Move Folder - GET (for modal)
    [HttpGet]
    public async Task<IActionResult> MoveFolder(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var folder = await _context.Folders.Include(f => f.SubFolders).FirstOrDefaultAsync(f => f.Id == id && f.UserId == user.Id);
        if (folder == null)
            return Forbid();
        var allFolders = await _context.Folders.Where(f => f.UserId == user.Id && f.Id != id).ToListAsync();
        // Exclude descendants
        var descendants = GetDescendantFolderIds(folder);
        var selectableFolders = allFolders.Where(f => !descendants.Contains(f.Id)).ToList();
        ViewBag.Folders = selectableFolders;
        ViewBag.CurrentParentId = folder.ParentFolderId;
        ViewBag.FolderId = id;
        return PartialView("_MoveFolderModal");
    }

    // Move Folder - POST
    [HttpPost]
    public async Task<IActionResult> MoveFolder(int id, int? targetParentFolderId)
    {
        var user = await _userManager.GetUserAsync(User);
        var folder = await _context.Folders.Include(f => f.SubFolders).FirstOrDefaultAsync(f => f.Id == id && f.UserId == user.Id);
        if (folder == null)
            return Forbid();
        if (targetParentFolderId == id || GetDescendantFolderIds(folder).Contains(targetParentFolderId ?? -1))
        {
            TempData["ErrorMessage"] = "Cannot move a folder into itself or its descendant.";
            return RedirectToAction("Index", new { folderId = folder.ParentFolderId });
        }
        if (targetParentFolderId.HasValue)
        {
            var targetFolder = await _context.Folders.FindAsync(targetParentFolderId);
            if (targetFolder == null || targetFolder.UserId != user.Id)
            {
                TempData["ErrorMessage"] = "Target folder not found or access denied.";
                return RedirectToAction("Index", new { folderId = folder.ParentFolderId });
            }
        }
        folder.ParentFolderId = targetParentFolderId;
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Folder moved successfully!";
        return RedirectToAction("Index", new { folderId = targetParentFolderId });
    }

    // Helper to get all descendant folder IDs (to prevent moving into self/descendant)
    private List<int> GetDescendantFolderIds(FolderModel folder)
    {
        var ids = new List<int>();
        if (folder.SubFolders != null)
        {
            foreach (var sub in folder.SubFolders)
            {
                ids.Add(sub.Id);
                ids.AddRange(GetDescendantFolderIds(sub));
            }
        }
        return ids;
    }

    [HttpPost]
    public async Task<IActionResult> BulkDelete(List<int> fileIds)
    {
        var user = await _userManager.GetUserAsync(User);
        var files = await _context.Files.Where(f => fileIds.Contains(f.Id) && f.UserId == user.Id).ToListAsync();
        foreach (var file in files)
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", file.FilePath.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
            _context.Files.Remove(file);
        }
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"{files.Count} file(s) deleted.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> BulkMove(List<int> fileIds, int? targetFolderId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (targetFolderId == null)
        {
            // Show modal to select target folder
            var folders = await _context.Folders.Where(f => f.UserId == user.Id).ToListAsync();
            ViewBag.Folders = folders;
            ViewBag.FileIds = fileIds;
            return PartialView("_BulkMoveModal");
        }
        var files = await _context.Files.Where(f => fileIds.Contains(f.Id) && f.UserId == user.Id).ToListAsync();
        foreach (var file in files)
        {
            file.FolderId = targetFolderId;
        }
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"{files.Count} file(s) moved.";
        return RedirectToAction("Index", new { folderId = targetFolderId });
    }

    [HttpGet]
    public async Task<IActionResult> BulkDownload(List<int> fileIds)
    {
        var user = await _userManager.GetUserAsync(User);
        var files = await _context.Files.Where(f => fileIds.Contains(f.Id) && (f.UserId == user.Id)).ToListAsync();
        if (!files.Any())
            return NotFound("No files found.");
        var zipName = $"files_{DateTime.Now:yyyyMMddHHmmss}.zip";
        using (var ms = new MemoryStream())
        {
            using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                foreach (var file in files)
                {
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", file.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        var entry = archive.CreateEntry(file.FileName);
                        using (var entryStream = entry.Open())
                        using (var fileStream = System.IO.File.OpenRead(filePath))
                        {
                            await fileStream.CopyToAsync(entryStream);
                        }
                    }
                }
            }
            ms.Position = 0;
            return File(ms.ToArray(), "application/zip", zipName);
        }
    }
}

