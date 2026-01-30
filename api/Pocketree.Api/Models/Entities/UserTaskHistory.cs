using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ADproject.Models.Entities
{
    public class UserTaskHistory
    {
        [Key]
        public int HistoryID { get; set; }
        public int UserID { get; set; }
        public int TaskID { get; set; }
        [Required, StringLength(20)]
        public string Status { get; set; }
        public DateTime CompletionDate { get; set; }
        // Navigation Property
        [ForeignKey("UserID")]
        public virtual User User { get; set; }
        [ForeignKey("TaskID")]
        public virtual Task Task { get; set; }
    }
}
