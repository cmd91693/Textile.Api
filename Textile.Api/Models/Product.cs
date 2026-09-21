namespace Textile.Api.Models
{
    public class Product
    {
        public int Id { get; set; }

        public string? ProductName { get; set; }

        public string? Category { get; set; }

        public decimal Price { get; set; }

        public int Quantity { get; set; }

        public bool IsActive { get; set; }
    }
}