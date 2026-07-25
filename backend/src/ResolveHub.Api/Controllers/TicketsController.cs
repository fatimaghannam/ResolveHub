using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Common;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize(Roles = RoleNames.Employee)]
public sealed class TicketsController(ITicketService ticketService)
    : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<TicketListItemDto>), 200)]
    public async Task<ActionResult<PagedResultDto<TicketListItemDto>>> GetTickets(
        [FromQuery] TicketFilterDto filter,
        CancellationToken cancellationToken)
    {
        return Ok(await ticketService.GetTicketsAsync(
            GetUserId(), filter, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TicketDetailsDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<TicketDetailsDto>> GetTicket(
        int id,
        CancellationToken cancellationToken)
    {
        var ticket = await ticketService.GetTicketAsync(
            GetUserId(), id, cancellationToken);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TicketDetailsDto), 201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<TicketDetailsDto>> CreateTicket(
        [FromBody] CreateTicketRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await ticketService.CreateTicketAsync(
            GetUserId(), request, cancellationToken);
        if (result.Status == TicketOperationStatus.Invalid)
            return BadRequest(new { message = result.Message });

        return CreatedAtAction(
            nameof(GetTicket),
            new { id = result.Value!.Id },
            result.Value);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(TicketDetailsDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<ActionResult<TicketDetailsDto>> UpdateTicket(
        int id,
        [FromBody] UpdateTicketRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await ticketService.UpdateTicketAsync(
            GetUserId(), id, request, cancellationToken);
        return result.Status switch
        {
            TicketOperationStatus.Success => Ok(result.Value),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Conflict =>
                Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };
    }

    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> CancelTicket(
        int id,
        [FromBody] CancelTicketRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await ticketService.CancelTicketAsync(
            GetUserId(), id, request, cancellationToken);
        return result.Status switch
        {
            TicketOperationStatus.Success => NoContent(),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Conflict =>
                Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };
    }

    private int GetUserId()
    {
        var value = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException(
                "The authenticated user identifier is invalid.");
    }
}
