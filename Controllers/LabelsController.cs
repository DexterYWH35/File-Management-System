using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FileManagementSystem.Data;
using FileManagementSystem.Models;
using System.Security.Claims;

namespace FileManagementSystem.Controllers
{
    [Authorize]
    public class LabelsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LabelsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Labels
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var labels = await _context.Labels
                .Where(l => l.UserId == userId)
                .ToListAsync();
            return View(labels);
        }

        // POST: Labels/Create
        [HttpPost]
        public async Task<IActionResult> Create(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest("Label name cannot be empty");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var label = new Label
            {
                Name = name,
                UserId = userId
            };

            _context.Labels.Add(label);
            await _context.SaveChangesAsync();

            return Ok(new { id = label.Id, name = label.Name });
        }

        // POST: Labels/AddToFile
        [HttpPost]
        public async Task<IActionResult> AddToFile(int fileId, int labelId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            // Verify file and label belong to the user
            var file = await _context.Files.FirstOrDefaultAsync(f => f.Id == fileId && f.UserId == userId);
            var label = await _context.Labels.FirstOrDefaultAsync(l => l.Id == labelId && l.UserId == userId);

            if (file == null || label == null)
            {
                return NotFound();
            }

            var fileLabel = new FileLabel
            {
                FileId = fileId,
                LabelId = labelId
            };

            _context.FileLabels.Add(fileLabel);
            await _context.SaveChangesAsync();

            return Ok();
        }

        // POST: Labels/RemoveFromFile
        [HttpPost]
        public async Task<IActionResult> RemoveFromFile(int fileId, int labelId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            // Verify file and label belong to the user
            var file = await _context.Files.FirstOrDefaultAsync(f => f.Id == fileId && f.UserId == userId);
            var label = await _context.Labels.FirstOrDefaultAsync(l => l.Id == labelId && l.UserId == userId);

            if (file == null || label == null)
            {
                return NotFound();
            }

            var fileLabel = await _context.FileLabels
                .FirstOrDefaultAsync(fl => fl.FileId == fileId && fl.LabelId == labelId);

            if (fileLabel != null)
            {
                _context.FileLabels.Remove(fileLabel);
                await _context.SaveChangesAsync();
            }

            return Ok();
        }

        // GET: Labels/GetFileLabels
        public async Task<IActionResult> GetFileLabels(int fileId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            // Verify file belongs to the user
            var file = await _context.Files.FirstOrDefaultAsync(f => f.Id == fileId && f.UserId == userId);
            if (file == null)
            {
                return NotFound();
            }

            var labels = await _context.FileLabels
                .Where(fl => fl.FileId == fileId)
                .Include(fl => fl.Label)
                .Select(fl => new { id = fl.Label.Id, name = fl.Label.Name })
                .ToListAsync();

            return Json(labels);
        }
    }
} 