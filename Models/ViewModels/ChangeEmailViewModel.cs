using System.ComponentModel.DataAnnotations;

namespace FileManagementSystem.Models.ViewModels
{
    public class ChangeEmailViewModel
    {
        [Required(ErrorMessage = "Current email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [Display(Name = "Current Email")]
        public string CurrentEmail { get; set; }

        [Required(ErrorMessage = "New email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [Display(Name = "New Email")]
        public string NewEmail { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        public string Password { get; set; }
    }
} 