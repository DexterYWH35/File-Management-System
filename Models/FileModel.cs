using System;
using System.ComponentModel.DataAnnotations;

namespace FileManagementSystem.Models
{
    public class FileModel
    {
        public int Id { get; set; }

        [Required]
        public string FileName { get; set; }

        [Required]
        public string FilePath { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        public DateTime UploadDate { get; set; } = DateTime.UtcNow;
    }
}
