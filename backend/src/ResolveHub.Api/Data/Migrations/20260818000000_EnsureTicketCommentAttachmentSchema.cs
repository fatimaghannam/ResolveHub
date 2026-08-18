using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ResolveHub.Api.Data;

#nullable disable

namespace ResolveHub.Api.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260818000000_EnsureTicketCommentAttachmentSchema")]
public sealed class EnsureTicketCommentAttachmentSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[TicketCommentAttachment]', N'U') IS NULL
            BEGIN
                CREATE TABLE [TicketCommentAttachment] (
                    [ID] int NOT NULL IDENTITY,
                    [TicketCommentID] int NOT NULL,
                    [UploadedByUserAccountID] int NOT NULL,
                    [FileName] nvarchar(255) NOT NULL,
                    [StoredFileName] nvarchar(255) NOT NULL,
                    [FilePath] nvarchar(500) NOT NULL,
                    [ContentType] nvarchar(150) NOT NULL,
                    [FileSizeBytes] bigint NOT NULL,
                    [UploadedDate] datetime2 NOT NULL,
                    CONSTRAINT [PK_TicketCommentAttachment] PRIMARY KEY ([ID]),
                    CONSTRAINT [FK_TicketCommentAttachment_TicketComment_TicketCommentID]
                        FOREIGN KEY ([TicketCommentID]) REFERENCES [TicketComment] ([ID]) ON DELETE CASCADE,
                    CONSTRAINT [FK_TicketCommentAttachment_UserAccount_UploadedByUserAccountID]
                        FOREIGN KEY ([UploadedByUserAccountID]) REFERENCES [UserAccount] ([ID])
                );
                CREATE INDEX [IX_TicketCommentAttachment_TicketCommentID]
                    ON [TicketCommentAttachment] ([TicketCommentID]);
                CREATE INDEX [IX_TicketCommentAttachment_UploadedByUserAccountID]
                    ON [TicketCommentAttachment] ([UploadedByUserAccountID]);
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DROP TABLE IF EXISTS [TicketCommentAttachment];");
    }
}
