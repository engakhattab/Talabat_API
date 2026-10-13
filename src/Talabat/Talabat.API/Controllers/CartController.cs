using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talabat.Application.Abstractions;
using Talabat.Customer.API.Auth;
using Talabat.Application.Basket.AddItem;
using Talabat.Application.Basket.ClearCart;
using Talabat.Application.Basket.GetCart;
using Talabat.Application.Basket.RemoveItem;
using Talabat.Application.Basket.UpdateQuantity;
using Talabat.Application.Common.Results;
using Talabat.Customer.API.Contracts.Cart;
using Talabat.Customer.API.Contracts.Common;
using Talabat.Customer.API.Extensions;
using Talabat.Customer.API.Middleware;

namespace Talabat.Customer.API.Controllers;

[ApiController]
[Route("api/me/cart")]
[Tags("Cart")]
[Authorize(Policy = AuthorizationPolicies.CustomerAccess)]
[RequireCustomerProfile]
[Produces("application/json")]
public sealed class CartController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly GetCartHandler _getCartHandler;
    private readonly AddCartItemHandler _addCartItemHandler;
    private readonly UpdateCartItemQuantityHandler _updateCartItemQuantityHandler;
    private readonly RemoveCartItemHandler _removeCartItemHandler;
    private readonly ClearCartHandler _clearCartHandler;

    public CartController(
        ICurrentUser currentUser,
        GetCartHandler getCartHandler,
        AddCartItemHandler addCartItemHandler,
        UpdateCartItemQuantityHandler updateCartItemQuantityHandler,
        RemoveCartItemHandler removeCartItemHandler,
        ClearCartHandler clearCartHandler)
    {
        _currentUser = currentUser;
        _getCartHandler = getCartHandler;
        _addCartItemHandler = addCartItemHandler;
        _updateCartItemQuantityHandler = updateCartItemQuantityHandler;
        _removeCartItemHandler = removeCartItemHandler;
        _clearCartHandler = clearCartHandler;
    }

    [HttpGet(Name = "GetCart")]
    [ProducesResponseType<CartResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetCart(CancellationToken cancellationToken)
    {
        var result = await _getCartHandler.Handle(
            new Application.Basket.GetCart.GetCartQuery(_currentUser.CustomerId!.Value),
            cancellationToken);

        return result.ToActionResult(cart => Ok(MapToResponse(cart)));
    }

    [HttpPost("items", Name = "AddCartItem")]
    [Consumes("application/json")]
    [ProducesResponseType<CartResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddItem(
        [FromBody] AddCartItemRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AddCartItemCommand(
            _currentUser.CustomerId!.Value,
            request.RestaurantId,
            request.ProductId,
            request.Quantity);

        var result = await _addCartItemHandler.Handle(command, cancellationToken);

        return result.ToActionResult(cart => Ok(MapToResponse(cart)));
    }

    [HttpPut("items/{productId:int}", Name = "UpdateCartItemQuantity")]
    [Consumes("application/json")]
    [ProducesResponseType<CartResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateItemQuantity(
        int productId,
        [FromBody] UpdateCartItemRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCartItemQuantityCommand(
            _currentUser.CustomerId!.Value,
            productId,
            request.Quantity);

        var result = await _updateCartItemQuantityHandler.Handle(command, cancellationToken);

        return result.ToActionResult(cart => Ok(MapToResponse(cart)));
    }

    [HttpDelete("items/{productId:int}", Name = "RemoveCartItem")]
    [ProducesResponseType<CartResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveItem(
        int productId,
        CancellationToken cancellationToken)
    {
        var command = new RemoveCartItemCommand(
            _currentUser.CustomerId!.Value,
            productId);

        var result = await _removeCartItemHandler.Handle(command, cancellationToken);

        return result.ToActionResult(cart => Ok(MapToResponse(cart)));
    }

    [HttpDelete(Name = "ClearCart")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ClearCart(CancellationToken cancellationToken)
    {
        var command = new ClearCartCommand(_currentUser.CustomerId!.Value);

        var result = await _clearCartHandler.Handle(command, cancellationToken);

        return result.ToActionResult(_ => NoContent());
    }

    private static CartResponse MapToResponse(Application.Basket.Models.CartDetails cart)
    {
        var items = cart.Items.Select(i => new CartLineItemDto(
            i.ProductId,
            i.ProductName,
            new MoneyDto(i.CurrentUnitPrice.Amount),
            i.Quantity,
            new MoneyDto(i.LineTotal.Amount))).ToList();

        return new CartResponse(
            cart.Id,
            cart.CustomerId,
            cart.RestaurantId,
            cart.Status,
            items,
            new MoneyDto(cart.CalculatedCurrentTotal.Amount));
    }
}
