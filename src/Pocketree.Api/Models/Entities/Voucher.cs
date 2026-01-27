using System.ComponentModel.DataAnnotations;
namespace ADproject.Models.Entities
{
    public class Voucher
    {
        [Key]
        public int VoucherID { get; set; }
        [Required, StringLength(50)]
        public string VoucherName { get; set; }
        [Required]
        public string Description { get; set; }
    }
}
