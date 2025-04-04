using System.ComponentModel.DataAnnotations;

namespace FileManagementSystem.Models.ViewModels
{
    public class ProfileViewModel
    {
        [Display(Name = "Username")]
        public string UserName { get; set; }

        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Role")]
        public string Role { get; set; }

        [Display(Name = "Total Files")]
        public int TotalFiles { get; set; }

        [Display(Name = "Storage Used")]
        public long StorageUsed { get; set; }
    }
}
