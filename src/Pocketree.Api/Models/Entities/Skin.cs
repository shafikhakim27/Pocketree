using System.ComponentModel.DataAnnotations;
namespace ADproject.Models.Entities
{
    public class Skin
    {
        [Key]
        public int SkinID { get; set; }
        [Required, StringLength(50)]
        public string SkinName { get; set; }
        [Required, StringLength(255)]
        public string ImageURL { get; set; }
    }
}
