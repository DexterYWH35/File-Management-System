using System.ComponentModel.DataAnnotations;

namespace FileManagementSystem.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "UserName is required")]
        // [EmailAddress(ErrorMessage = "Invalid email address")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public bool RememberMe { get; set; }
    }
}
