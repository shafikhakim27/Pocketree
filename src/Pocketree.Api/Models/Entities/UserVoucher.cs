using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace ADproject.Models.Entities
{
    public class UserVoucher
    {
        [Key]
        public int UserVoucherID { get; set; }
        [ForeignKey("UserID")]
        public int UserID { get; set; }
        [ForeignKey("VoucherID")]
        public int VoucherID { get; set; }
        [ForeignKey("LevelReachedID")]
        public int LevelReachedID { get; set; }
        [Required, StringLength(20)]
        public string RedemptionCode { get; set; }
    }
}
