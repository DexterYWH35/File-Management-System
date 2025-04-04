using Microsoft.AspNetCore.Identity;

namespace FileManagementSystem.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        public List<UserViewModel> Users { get; set; } = new List<UserViewModel>();
        public List<FileViewModel> Files { get; set; } = new List<FileViewModel>();
        public Dictionary<string, int> FileCountByUser { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, long> StorageUsageByUser { get; set; } = new Dictionary<string, long>();
    }

    public class UserViewModel
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public int FileCount { get; set; }
        public long TotalStorageUsed { get; set; }
        public bool IsAdmin { get; set; }
    }
} 