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

    public class FileViewModel
{
    public int Id { get; set; }
    public string FileName { get; set; }
    public string UserId { get; set; } 
    public string UserName { get; set; } // This is from AspNetUsers
    public DateTime UploadDate { get; set; }
}

}
