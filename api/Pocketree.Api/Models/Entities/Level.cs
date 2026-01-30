using System.ComponentModel.DataAnnotations;

namespace ADproject.Models.Entities
{
    public class Level
    {
        [Key]
        public int LevelID { get; set; }
        [Required, StringLength(20)]
        public string LevelName { get; set; }
        public int MinCoins { get; set; }
        [StringLength(255)]
        public string LevelImageURL { get; set; }
    }
}
