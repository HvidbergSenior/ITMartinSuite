public class UnknownGroup
{
    public string Description { get; set; } = "";
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }

    public SubCategory? SelectedSubCategory { get; set; } // 👈 NEW
}