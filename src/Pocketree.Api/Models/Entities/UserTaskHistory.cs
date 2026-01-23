using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ADproject.Models.Entities
{
    public class UserTaskHistory
    {
        [Key]
        public int HistoryID { get; set; }
        [ForeignKey("UserID")]
        public int UserID { get; set; }
        [ForeignKey("TaskID")]
        public int TaskID { get; set; }
        [Required, StringLength(20)]
        public string Status { get; set; }
        public DateTime CompletionDate { get; set; }
        // Navigation Property
        public virtual User User { get; set; }
        public virtual Task Task { get; set; }
    }
}
