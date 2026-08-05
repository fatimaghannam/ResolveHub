using Microsoft.EntityFrameworkCore;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Admin;
using ResolveHub.Api.Entities;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Implementations;

public sealed class AdminCategoryService(ApplicationDbContext dbContext) : IAdminCategoryService
{
    private static readonly string[] ActiveStatuses =
    [TicketStatusNames.Open, TicketStatusNames.Assigned,
        TicketStatusNames.InProgress, TicketStatusNames.Pending];

    public async Task<TicketServiceResult<IReadOnlyCollection<AdminCategoryDto>>> GetAsync(
        AdminCategoryFilterDto filter, CancellationToken token)
    {
        var status = filter.Status?.Trim();
        if (!string.IsNullOrEmpty(status) && status is not ("Active" or "Inactive"))
            return new(TicketOperationStatus.Invalid, Message: "The selected status is invalid.");

        var query = dbContext.TicketCategories.AsNoTracking();
        var search = filter.Search?.Trim().ToLower();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(category => category.Name.ToLower().Contains(search) ||
                (category.Description != null && category.Description.ToLower().Contains(search)));
        if (status == "Active") query = query.Where(category => category.IsActive);
        if (status == "Inactive") query = query.Where(category => !category.IsActive);

        IReadOnlyCollection<AdminCategoryDto> categories = await query
            .OrderBy(category => category.SortOrder).ThenBy(category => category.Name)
            .Select(category => new AdminCategoryDto(category.ID, category.Name,
                category.Description ?? "",
                category.Tickets.Count(ticket => !ticket.IsDeleted &&
                    ActiveStatuses.Contains(ticket.TicketStatus.Name)), category.IsActive))
            .ToListAsync(token);
        return new(TicketOperationStatus.Success, categories);
    }

    public async Task<TicketServiceResult<AdminCategoryDto>> CreateAsync(
        int administratorId, SaveAdminCategoryRequestDto request, CancellationToken token)
    {
        var validation = Validate(request);
        if (validation is not null) return new(TicketOperationStatus.Invalid, Message: validation);
        var name = request.Name.Trim();
        var description = request.Description.Trim();
        if (await DuplicateExists(name, null, token))
            return new(TicketOperationStatus.Conflict,
                Message: "A category with this name already exists.");

        var now = DateTime.UtcNow;
        var nextSortOrder = (await dbContext.TicketCategories.MaxAsync(
            category => (int?)category.SortOrder, token) ?? 0) + 1;
        var category = new TicketCategory
        {
            Name = name, Description = description, SortOrder = nextSortOrder, IsActive = true
        };
        await using var transaction = await dbContext.Database.BeginTransactionAsync(token);
        dbContext.TicketCategories.Add(category);
        await dbContext.SaveChangesAsync(token);
        AddAudit(administratorId, category, "Category Created", null, "Active",
            $"Created the {category.Name} ticket category.", now);
        await dbContext.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(TicketOperationStatus.Success,
            new(category.ID, category.Name, description, 0, true));
    }

    public async Task<TicketServiceResult<AdminCategoryDto>> UpdateAsync(
        int administratorId, int categoryId, SaveAdminCategoryRequestDto request,
        CancellationToken token)
    {
        var validation = Validate(request);
        if (validation is not null) return new(TicketOperationStatus.Invalid, Message: validation);
        var category = await dbContext.TicketCategories.SingleOrDefaultAsync(
            item => item.ID == categoryId, token);
        if (category is null) return new(TicketOperationStatus.NotFound);
        var name = request.Name.Trim();
        var description = request.Description.Trim();
        if (await DuplicateExists(name, categoryId, token))
            return new(TicketOperationStatus.Conflict,
                Message: "A category with this name already exists.");

        var oldValue = $"{category.Name}: {category.Description}";
        category.Name = name;
        category.Description = description;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(token);
        AddAudit(administratorId, category, "Category Updated", oldValue,
            $"{name}: {description}", $"Updated the {name} ticket category.", DateTime.UtcNow);
        await dbContext.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        var activeTickets = await ActiveTicketCount(categoryId, token);
        return new(TicketOperationStatus.Success,
            new(category.ID, name, description, activeTickets, category.IsActive));
    }

    public async Task<TicketServiceResult<AdminCategoryDto>> SetStatusAsync(
        int administratorId, int categoryId, bool isActive, CancellationToken token)
    {
        var category = await dbContext.TicketCategories.SingleOrDefaultAsync(
            item => item.ID == categoryId, token);
        if (category is null) return new(TicketOperationStatus.NotFound);
        if (category.IsActive != isActive)
        {
            var oldValue = category.IsActive ? "Active" : "Inactive";
            var newValue = isActive ? "Active" : "Inactive";
            category.IsActive = isActive;
            await using var transaction = await dbContext.Database.BeginTransactionAsync(token);
            AddAudit(administratorId, category,
                isActive ? "Category Activated" : "Category Deactivated",
                oldValue, newValue,
                $"The {category.Name} ticket category was {(isActive ? "activated" : "deactivated")}.",
                DateTime.UtcNow);
            await dbContext.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        }
        return new(TicketOperationStatus.Success,
            new(category.ID, category.Name, category.Description ?? "",
                await ActiveTicketCount(categoryId, token), category.IsActive));
    }

    private async Task<bool> DuplicateExists(string name, int? excludedId,
        CancellationToken token)
    {
        var normalized = name.ToLower();
        return await dbContext.TicketCategories.AnyAsync(category =>
            (!excludedId.HasValue || category.ID != excludedId.Value) &&
            category.Name.ToLower() == normalized, token);
    }

    private async Task<int> ActiveTicketCount(int categoryId, CancellationToken token) =>
        await dbContext.Tickets.CountAsync(ticket => ticket.TicketCategoryID == categoryId &&
            !ticket.IsDeleted && ActiveStatuses.Contains(ticket.TicketStatus.Name), token);

    private static string? Validate(SaveAdminCategoryRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "Category Name is required.";
        if (string.IsNullOrWhiteSpace(request.Description)) return "Description is required.";
        if (request.Name.Trim().Length > 100) return "Category Name cannot exceed 100 characters.";
        return request.Description.Trim().Length > 500
            ? "Description cannot exceed 500 characters." : null;
    }

    private void AddAudit(int administratorId, TicketCategory category, string action,
        string? oldValue, string? newValue, string description, DateTime createdAt) =>
        dbContext.ActivityLogs.Add(new ActivityLog
        {
            PerformedByUserAccountID = administratorId,
            ActionType = action,
            EntityType = "TicketCategory",
            EntityID = category.ID.ToString(),
            Description = description,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedDate = createdAt
        });
}
