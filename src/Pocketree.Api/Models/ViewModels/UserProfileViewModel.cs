namespace ADproject.Models.ViewModels
{
    public class UserProfileViewModel
    {
        public string Username { get; set; }
        public int TotalCoins { get; set; }
        public int LevelID { get; set; }
        public string LevelName { get; set; }
        public string TreeImageURL { get; set; }
        public bool IsWithered { get; set; }
    }
}
