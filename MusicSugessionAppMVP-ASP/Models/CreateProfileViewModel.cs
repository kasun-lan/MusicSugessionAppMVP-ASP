using System.ComponentModel.DataAnnotations;

namespace MusicSugessionAppMVP_ASP.Models
{
    public class CreateProfileViewModel
    {
        [Required(ErrorMessage = "Name is required")]
        [Display(Name = "Name")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Preferred streaming platform is required")]
        [Display(Name = "Preferred streaming platform")]
        public Guid PreferredStreamingPlatformId { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        [Display(Name = "Choose password")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Please re-type your password")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        [Display(Name = "Re-type password")]
        public string ConfirmPassword { get; set; }

        public List<StreamingPlatformOption> StreamingPlatforms { get; set; } = new();
    }

    public class StreamingPlatformOption
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }
}

