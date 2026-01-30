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
        [ForeignKey("SkinID")]
        public int SkinID { get; set; }
        public DateTime RedemptionDate { get; set; }
        public bool IsEquipped { get; set; } = false;
    }
}
