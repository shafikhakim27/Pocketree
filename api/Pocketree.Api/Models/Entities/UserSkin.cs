using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace ADproject.Models.Entities
{
    public class UserSkin
    {
        [Key]
        public int UserSkinID { get; set; }
        [ForeignKey("UserID")]
        public int UserID { get; set; }
        public int SkinID { get; set; }
        public DateTime? RedemptionDate { get; set; }
        public bool IsRedeemed { get; set; } = false;
        public bool IsEquipped { get; set; } = false;

        // Navigation Property
        [ForeignKey("SkinID")]
        public virtual Skin? Skin { get; set; }
    }
}
