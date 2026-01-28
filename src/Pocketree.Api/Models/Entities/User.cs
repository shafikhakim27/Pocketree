using Microsoft.Extensions.Configuration.UserSecrets;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ADproject.Models.Entities
{
    public class User
    {
        [Key]
        public int UserID { get; set; }
        [Required, StringLength(50)]
        public string Username { get; set; }
        [Required, StringLength(255)]
        public string PasswordHash { get; set; }
        public string ProfileImageURL { get; set; } = "/images/default-user.jpg";
        public int TotalCoins { get; set; }
        public int CurrentLevelID { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public DateTime? LastActivityDate { get; set; }
        public string Email { get; set; }
        
        // Navigation Properties
        [ForeignKey("CurrentLevelID")]
        public virtual Level CurrentLevel { get; set; }
        public virtual ICollection<UserTaskHistory> TaskHistory { get; set; }
        public virtual ICollection<UserSkin> UserSkins { get; set; }
        public virtual ICollection<UserVoucher> UserVouchers { get; set; }
        public virtual UserSettings? Settings { get; set; }
        public virtual ICollection<Tree> Trees { get; set; }
        public virtual ICollection<UserBadge> UserBadges { get; set; }
    }
}
