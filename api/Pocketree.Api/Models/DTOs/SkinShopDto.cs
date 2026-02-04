namespace Pocketree.Api.Models.DTOs
{
    public class SkinShopDto
    {
        public int SkinID { get; set; }
        public string SkinName { get; set; }
        public int SkinPrice { get; set; }
        public string ImageURL { get; set; }
        public bool IsRedeemed { get; set; } 
        public bool IsEquipped { get; set; }
    }
}
