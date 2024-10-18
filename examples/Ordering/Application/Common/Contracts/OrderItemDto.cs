namespace Ordering.Application.Common.Contracts;

public class OrderItemDto
{
    public long Id { get; set; }
    public ProductDto Product { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
}
