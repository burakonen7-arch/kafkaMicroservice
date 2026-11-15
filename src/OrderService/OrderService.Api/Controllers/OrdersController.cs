using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using OrderService.Application;
namespace OrderService.Api.Controllers;

[ApiController, Route("api/orders")]
public sealed class OrdersController(IOrderService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ProductId)) return BadRequest("ProductId is required.");
        var order = await service.CreateAsync(request.ProductId.Trim(), request.Quantity, ct);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var order = await service.GetAsync(id, ct); return order is null ? NotFound() : Ok(order);
    }
}
public sealed record CreateOrderRequest([Required, StringLength(100)] string ProductId, [Range(1, int.MaxValue)] int Quantity);
