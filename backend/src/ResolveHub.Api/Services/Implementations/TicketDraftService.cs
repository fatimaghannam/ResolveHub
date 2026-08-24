using Microsoft.EntityFrameworkCore;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Entities;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Implementations;

public sealed class TicketDraftService(
    ApplicationDbContext dbContext,
    ITicketService ticketService,
    ILogger<TicketDraftService> logger) : ITicketDraftService
{
    public async Task<IReadOnlyCollection<TicketDraftDto>> GetAllAsync(
        int userId, CancellationToken token)
    {
        try
        {
            var ownedDrafts = dbContext.TicketDrafts
                .AsNoTracking()
                .Where(draft => draft.UserAccountID == userId)
                .OrderByDescending(draft => draft.UpdatedDate);

            return await Project(ownedDrafts).ToListAsync(token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Failed to load ticket drafts for authenticated user {UserId}.",
                userId);
            throw;
        }
    }

    public Task<TicketDraftDto?> GetAsync(int userId, int id, CancellationToken token) =>
        Project(dbContext.TicketDrafts.AsNoTracking()
            .Where(draft => draft.ID == id && draft.UserAccountID == userId))
            .SingleOrDefaultAsync(token);

    public async Task<TicketServiceResult<TicketDraftDto>> CreateAsync(
        int userId, SaveTicketDraftRequestDto request, CancellationToken token)
    {
        var validation = await ValidateAsync(request, token);
        if (validation is not null)
            return new(TicketOperationStatus.Invalid, Message: validation);
        var now = DateTime.UtcNow;
        var draft = new TicketDraft { UserAccountID = userId, CreatedDate = now, UpdatedDate = now };
        Apply(draft, request);
        dbContext.TicketDrafts.Add(draft);
        await dbContext.SaveChangesAsync(token);
        return new(TicketOperationStatus.Success, ToDto(draft));
    }

    public async Task<TicketServiceResult<TicketDraftDto>> UpdateAsync(
        int userId, int id, SaveTicketDraftRequestDto request, CancellationToken token)
    {
        var draft = await dbContext.TicketDrafts.SingleOrDefaultAsync(
            item => item.ID == id && item.UserAccountID == userId, token);
        if (draft is null) return new(TicketOperationStatus.NotFound);
        var validation = await ValidateAsync(request, token);
        if (validation is not null)
            return new(TicketOperationStatus.Invalid, Message: validation);
        Apply(draft, request);
        draft.UpdatedDate = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(token);
        return new(TicketOperationStatus.Success, ToDto(draft));
    }

    public async Task<bool> DeleteAsync(int userId, int id, CancellationToken token)
    {
        var draft = await dbContext.TicketDrafts.SingleOrDefaultAsync(
            item => item.ID == id && item.UserAccountID == userId, token);
        if (draft is null) return false;
        dbContext.TicketDrafts.Remove(draft);
        await dbContext.SaveChangesAsync(token);
        return true;
    }

    public async Task<TicketServiceResult<TicketDetailsDto>> SubmitAsync(
        int userId, int id, CancellationToken token)
    {
        var draft = await dbContext.TicketDrafts.SingleOrDefaultAsync(
            item => item.ID == id && item.UserAccountID == userId, token);
        if (draft is null) return new(TicketOperationStatus.NotFound);
        var request = new CreateTicketRequestDto
        {
            Title = draft.Title ?? string.Empty,
            Description = draft.Description ?? string.Empty,
            TicketCategoryId = draft.TicketCategoryID ?? 0,
            TicketPriorityId = draft.TicketPriorityID ?? 0
        };
        if (!dbContext.Database.IsRelational() ||
            dbContext.Database.CurrentTransaction is not null)
            return await SubmitCoreAsync(draft, userId, request, token);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async strategyToken =>
        {
            dbContext.ChangeTracker.Clear();
            var currentDraft = await dbContext.TicketDrafts.SingleOrDefaultAsync(
                item => item.ID == id && item.UserAccountID == userId, strategyToken);
            if (currentDraft is null) return new(TicketOperationStatus.NotFound);
            await using var transaction =
                await dbContext.Database.BeginTransactionAsync(strategyToken);
            try
            {
                var result = await SubmitCoreAsync(
                    currentDraft, userId, request, strategyToken);
                if (result.Status == TicketOperationStatus.Success)
                    await transaction.CommitAsync(strategyToken);
                else
                    await transaction.RollbackAsync(CancellationToken.None);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }, token);
    }

    private async Task<TicketServiceResult<TicketDetailsDto>> SubmitCoreAsync(
        TicketDraft draft, int userId, CreateTicketRequestDto request,
        CancellationToken token)
    {
        var result = await ticketService.CreateTicketAsync(
            userId, request, token);
        if (result.Status != TicketOperationStatus.Success)
            return result;

        dbContext.TicketDrafts.Remove(draft);
        await dbContext.SaveChangesAsync(token);
        return result;
    }

    private async Task<string?> ValidateAsync(
        SaveTicketDraftRequestDto request, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(request.Title) &&
            string.IsNullOrWhiteSpace(request.Description) &&
            request.TicketCategoryId is null &&
            request.TicketPriorityId is null)
            return "Enter at least one value before saving a draft.";
        if (request.TicketCategoryId is int categoryId &&
            !await dbContext.TicketCategories.AnyAsync(
                item => item.ID == categoryId && item.IsActive, token))
            return "Select a valid active category.";
        if (request.TicketPriorityId is int priorityId &&
            !await dbContext.TicketPriorities.AnyAsync(
                item => item.ID == priorityId && item.IsActive, token))
            return "Select a valid active priority.";
        return null;
    }

    private static void Apply(TicketDraft draft, SaveTicketDraftRequestDto request)
    {
        draft.Title = NullIfWhiteSpace(request.Title);
        draft.Description = NullIfWhiteSpace(request.Description);
        draft.TicketCategoryID = request.TicketCategoryId;
        draft.TicketPriorityID = request.TicketPriorityId;
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static TicketDraftDto ToDto(TicketDraft draft) =>
        new(draft.ID, draft.Title, draft.Description, draft.TicketCategoryID,
            draft.TicketPriorityID, draft.CreatedDate, draft.UpdatedDate);
    private static IQueryable<TicketDraftDto> Project(IQueryable<TicketDraft> query) =>
        query.Select(draft => new TicketDraftDto(draft.ID, draft.Title,
            draft.Description, draft.TicketCategoryID, draft.TicketPriorityID,
            draft.CreatedDate, draft.UpdatedDate));
}
