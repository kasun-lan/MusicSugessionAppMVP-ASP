using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace MusicSugessionAppMVP_ASP.Models
{
    public class ProfileViewModel
    {
        public string Name { get; set; }
        public string Email { get; set; }

        public Guid PreferredStreamingPlatformId { get; set; }

        [ValidateNever]
        public List<StreamingPlatformOption> StreamingPlatforms { get; set; }

        public string? NewPassword { get; set; }
        public string? ConfirmPassword { get; set; }
    }
}
