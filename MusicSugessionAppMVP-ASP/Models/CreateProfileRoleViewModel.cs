using System.ComponentModel.DataAnnotations;

namespace MusicSugessionAppMVP_ASP.Models
{
    public class CreateProfileRoleViewModel
    {
        [Required]
        public Guid UserId { get; set; }

        [Display(Name = "I'm a DJ")]
        public bool IsDJ { get; set; }

        [Display(Name = "I'm a musician")]
        public bool IsMusician { get; set; }

        [Display(Name = "I'm a producer")]
        public bool IsProducer { get; set; }

        [Display(Name = "I professionally curate playlists")]
        public bool IsProfessionalCurator { get; set; }
    }
}

