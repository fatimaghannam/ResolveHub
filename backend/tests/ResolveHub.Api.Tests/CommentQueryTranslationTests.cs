using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ResolveHub.Api.Data;
using ResolveHub.Api.Data.Migrations;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Entities;
using ResolveHub.Api.Services.Implementations;
using Xunit;

namespace ResolveHub.Api.Tests;

public sealed class CommentQueryTranslationTests
{
    [Fact]
    public void AttachmentSchemaRepair_CreatesMissingTableAndIndexes()
    {
        var migration = new EnsureTicketCommentAttachmentSchema();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var method = typeof(EnsureTicketCommentAttachmentSchema).GetMethod(
            "Up", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method.Invoke(migration, [builder]);

        var sql = Assert.Single(builder.Operations.OfType<SqlOperation>()).Sql;
        Assert.Contains("OBJECT_ID(N'[TicketCommentAttachment]'", sql,
            StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [TicketCommentAttachment]", sql,
            StringComparison.Ordinal);
        Assert.Contains("IX_TicketCommentAttachment_TicketCommentID", sql,
            StringComparison.Ordinal);
        Assert.Contains("IX_TicketCommentAttachment_UploadedByUserAccountID", sql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PagedThreadProjection_IsTranslatableBySqlServer()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(local);Database=TranslationOnly;Trusted_Connection=True;")
            .Options;
        using var context = new ApplicationDbContext(options);
        var service = new TicketCommentService(context);
        var method = typeof(TicketCommentService).GetMethod(
            "ProjectQuery", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var query = Assert.IsAssignableFrom<IQueryable<TicketCommentDto>>(
            method.Invoke(service, [new Ticket
            {
                ID = 42,
                CreatedByUserAccountID = 7,
                AssignedToUserAccountID = 9
            }, 7, new List<int> { 1, 2, 3 }]));

        var sql = query.ToQueryString();

        Assert.Contains("TicketComment", sql, StringComparison.Ordinal);
        Assert.Contains("ParentCommentID", sql, StringComparison.Ordinal);
    }
}
