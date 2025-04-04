using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

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

        public virtual ICollection<FileLabel> FileLabels { get; set; } = new List<FileLabel>();
    }

    public class Label
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string UserId { get; set; }

        public virtual ICollection<FileLabel> FileLabels { get; set; } = new List<FileLabel>();
    }

    public class FileLabel
    {
        public int FileId { get; set; }
        public virtual FileModel File { get; set; }

        public int LabelId { get; set; }
        public virtual Label Label { get; set; }
    }

    public class FileViewModel
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string UserId { get; set; } 
        public string UserName { get; set; }
        public DateTime UploadDate { get; set; }
        public List<string> Labels { get; set; } = new List<string>();
        public string FileType { get; set; }
        public long FileSize { get; set; }
    }
}
