using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using StockService.Application;
namespace StockService.Api.Controllers;

[ApiController, Route("api/stocks")]
public sealed class StocksController(IStockService service) : ControllerBase
{
    [HttpGet("{productId}")]
    public async Task<IActionResult> Get(string productId, CancellationToken ct)
    {
        var stock = await service.GetAsync(productId, ct); return stock is null ? NotFound() : Ok(stock);
    }
    [HttpPut("{productId}")]
    public async Task<IActionResult> Set(string productId, SetStockRequest request, CancellationToken ct) => Ok(await service.SetAsync(productId, request.Quantity, ct));
    [HttpGet("events")]
    public async Task<IActionResult> Events(CancellationToken ct) => Ok(await service.EventsAsync(ct));
}
public sealed record SetStockRequest([Range(0, int.MaxValue)] int Quantity);
