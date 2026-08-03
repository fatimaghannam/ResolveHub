/*
    ResolveHub full ticket-data cleanup.

    REVIEW ONLY until explicitly approved. This script permanently removes all
    ticket and ticket-dependent data while preserving users, roles, departments,
    categories, priorities, statuses, Identity tables, and non-ticket ActivityLog
    records.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.Ticket', N'U') IS NULL
    THROW 51000, 'Expected table dbo.Ticket was not found. No changes were made.', 1;

DECLARE @UnexpectedTicketForeignKeys nvarchar(max);

SELECT @UnexpectedTicketForeignKeys = STRING_AGG(
    QUOTENAME(OBJECT_SCHEMA_NAME(fkc.parent_object_id)) + N'.' +
    QUOTENAME(OBJECT_NAME(fkc.parent_object_id)) + N'.' +
    QUOTENAME(COL_NAME(fkc.parent_object_id, fkc.parent_column_id)), N', ')
FROM sys.foreign_key_columns AS fkc
WHERE fkc.referenced_object_id = OBJECT_ID(N'dbo.Ticket')
  AND OBJECT_NAME(fkc.parent_object_id) NOT IN
  (
      N'Ticket',
      N'TicketAssignmentRequest',
      N'TicketAttachment',
      N'TicketComment',
      N'TicketHistory',
      N'TicketWorkSession',
      N'DuplicateReview'
  );

IF @UnexpectedTicketForeignKeys IS NOT NULL
BEGIN
    DECLARE @ForeignKeyError nvarchar(2048) =
        N'Unexpected foreign keys reference dbo.Ticket: ' +
        @UnexpectedTicketForeignKeys + N'. No changes were made.';
    THROW 51001, @ForeignKeyError, 1;
END;

DECLARE @DeletedRows table
(
    [Order] int IDENTITY(1, 1) NOT NULL,
    TableName sysname NOT NULL,
    RowsAffected int NOT NULL
);

BEGIN TRY
    BEGIN TRANSACTION;

    DELETE FROM dbo.TicketCommentAttachment;
    INSERT INTO @DeletedRows (TableName, RowsAffected)
    VALUES (N'TicketCommentAttachment', @@ROWCOUNT);

    DELETE FROM dbo.TicketComment;
    INSERT INTO @DeletedRows (TableName, RowsAffected)
    VALUES (N'TicketComment', @@ROWCOUNT);

    DELETE FROM dbo.TicketAttachment;
    INSERT INTO @DeletedRows (TableName, RowsAffected)
    VALUES (N'TicketAttachment', @@ROWCOUNT);

    DELETE FROM dbo.TicketHistory;
    INSERT INTO @DeletedRows (TableName, RowsAffected)
    VALUES (N'TicketHistory', @@ROWCOUNT);

    DELETE FROM dbo.TicketWorkSession;
    INSERT INTO @DeletedRows (TableName, RowsAffected)
    VALUES (N'TicketWorkSession', @@ROWCOUNT);

    DELETE FROM dbo.TicketAssignmentRequest;
    INSERT INTO @DeletedRows (TableName, RowsAffected)
    VALUES (N'TicketAssignmentRequest', @@ROWCOUNT);

    DELETE FROM dbo.DuplicateReview;
    INSERT INTO @DeletedRows (TableName, RowsAffected)
    VALUES (N'DuplicateReview', @@ROWCOUNT);

    DELETE FROM dbo.TicketDraft;
    INSERT INTO @DeletedRows (TableName, RowsAffected)
    VALUES (N'TicketDraft', @@ROWCOUNT);

    DELETE FROM dbo.ActivityLog
    WHERE EntityType = N'Ticket';
    INSERT INTO @DeletedRows (TableName, RowsAffected)
    VALUES (N'ActivityLog (ticket only)', @@ROWCOUNT);

    DELETE FROM dbo.UserNotification
    WHERE TicketReferenceNumber IS NOT NULL;
    INSERT INTO @DeletedRows (TableName, RowsAffected)
    VALUES (N'UserNotification (ticket only)', @@ROWCOUNT);

    UPDATE dbo.Ticket
    SET OriginalTicketID = NULL
    WHERE OriginalTicketID IS NOT NULL;
    INSERT INTO @DeletedRows (TableName, RowsAffected)
    VALUES (N'Ticket self-references cleared', @@ROWCOUNT);

    DELETE FROM dbo.Ticket;
    INSERT INTO @DeletedRows (TableName, RowsAffected)
    VALUES (N'Ticket', @@ROWCOUNT);

    IF EXISTS (SELECT 1 FROM dbo.Ticket)
        THROW 51002, 'Ticket verification failed. The transaction will be rolled back.', 1;
    IF EXISTS (SELECT 1 FROM dbo.TicketHistory)
        THROW 51003, 'TicketHistory verification failed. The transaction will be rolled back.', 1;
    IF EXISTS (SELECT 1 FROM dbo.TicketComment)
        THROW 51004, 'TicketComment verification failed. The transaction will be rolled back.', 1;
    IF EXISTS (SELECT 1 FROM dbo.TicketCommentAttachment)
        THROW 51005, 'TicketCommentAttachment verification failed. The transaction will be rolled back.', 1;
    IF EXISTS (SELECT 1 FROM dbo.TicketAttachment)
        THROW 51006, 'TicketAttachment verification failed. The transaction will be rolled back.', 1;
    IF EXISTS (SELECT 1 FROM dbo.TicketWorkSession)
        THROW 51007, 'TicketWorkSession verification failed. The transaction will be rolled back.', 1;
    IF EXISTS (SELECT 1 FROM dbo.TicketAssignmentRequest)
        THROW 51008, 'TicketAssignmentRequest verification failed. The transaction will be rolled back.', 1;
    IF EXISTS (SELECT 1 FROM dbo.DuplicateReview)
        THROW 51009, 'DuplicateReview verification failed. The transaction will be rolled back.', 1;
    IF EXISTS (SELECT 1 FROM dbo.TicketDraft)
        THROW 51010, 'TicketDraft verification failed. The transaction will be rolled back.', 1;
    IF EXISTS (SELECT 1 FROM dbo.ActivityLog WHERE EntityType = N'Ticket')
        THROW 51011, 'Ticket ActivityLog verification failed. The transaction will be rolled back.', 1;
    IF EXISTS (SELECT 1 FROM dbo.UserNotification WHERE TicketReferenceNumber IS NOT NULL)
        THROW 51012, 'Ticket UserNotification verification failed. The transaction will be rolled back.', 1;

    DBCC CHECKIDENT (N'dbo.TicketCommentAttachment', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT (N'dbo.TicketComment', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT (N'dbo.TicketAttachment', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT (N'dbo.TicketHistory', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT (N'dbo.TicketWorkSession', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT (N'dbo.TicketAssignmentRequest', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT (N'dbo.DuplicateReview', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT (N'dbo.TicketDraft', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT (N'dbo.Ticket', RESEED, 0) WITH NO_INFOMSGS;

    COMMIT TRANSACTION;

    SELECT TableName, RowsAffected
    FROM @DeletedRows
    ORDER BY [Order];

    SELECT
        (SELECT COUNT_BIG(*) FROM dbo.Ticket) AS TicketCount,
        (SELECT COUNT_BIG(*) FROM dbo.TicketHistory) AS TicketHistoryCount,
        (SELECT COUNT_BIG(*) FROM dbo.TicketComment) AS TicketCommentCount,
        (SELECT COUNT_BIG(*) FROM dbo.TicketCommentAttachment) AS TicketCommentAttachmentCount,
        (SELECT COUNT_BIG(*) FROM dbo.TicketAttachment) AS TicketAttachmentCount,
        (SELECT COUNT_BIG(*) FROM dbo.TicketWorkSession) AS TicketWorkSessionCount,
        (SELECT COUNT_BIG(*) FROM dbo.TicketAssignmentRequest) AS TicketAssignmentRequestCount,
        (SELECT COUNT_BIG(*) FROM dbo.DuplicateReview) AS DuplicateReviewCount,
        (SELECT COUNT_BIG(*) FROM dbo.TicketDraft) AS TicketDraftCount,
        (SELECT COUNT_BIG(*) FROM dbo.ActivityLog WHERE EntityType = N'Ticket') AS TicketActivityLogCount,
        (SELECT COUNT_BIG(*) FROM dbo.UserNotification WHERE TicketReferenceNumber IS NOT NULL) AS TicketNotificationCount;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    SELECT TableName, RowsAffected
    FROM @DeletedRows
    ORDER BY [Order];

    SELECT
        ERROR_NUMBER() AS ErrorNumber,
        ERROR_LINE() AS ErrorLine,
        ERROR_MESSAGE() AS ErrorMessage;

    THROW;
END CATCH;
