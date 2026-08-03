SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DeletedRows TABLE
(
    DeleteOrder int IDENTITY(1, 1) PRIMARY KEY,
    TableName sysname NOT NULL,
    RowsDeleted int NOT NULL
);

BEGIN TRY
    BEGIN TRANSACTION;

    CREATE TABLE #TargetTicket
    (
        ID int NOT NULL PRIMARY KEY,
        TicketReferenceNumber nvarchar(64) NOT NULL UNIQUE
    );

    INSERT INTO #TargetTicket (ID, TicketReferenceNumber)
    SELECT ID, TicketReferenceNumber
    FROM dbo.Ticket WITH (UPDLOCK, HOLDLOCK)
    WHERE TicketReferenceNumber LIKE N'RH-2026-9%';

    IF EXISTS
    (
        SELECT 1
        FROM dbo.Ticket AS ticket
        WHERE ticket.OriginalTicketID IN (SELECT ID FROM #TargetTicket)
          AND ticket.ID NOT IN (SELECT ID FROM #TargetTicket)
    )
        THROW 51000,
            'A non-demo ticket references a target ticket. No rows were deleted.',
            1;

    DELETE commentAttachment
    FROM dbo.TicketCommentAttachment AS commentAttachment
    INNER JOIN dbo.TicketComment AS comment
        ON comment.ID = commentAttachment.TicketCommentID
    INNER JOIN #TargetTicket AS target
        ON target.ID = comment.TicketID;
    INSERT INTO @DeletedRows (TableName, RowsDeleted)
    VALUES (N'TicketCommentAttachment', @@ROWCOUNT);

    DELETE duplicateReview
    FROM dbo.DuplicateReview AS duplicateReview
    WHERE duplicateReview.TicketID IN (SELECT ID FROM #TargetTicket)
       OR duplicateReview.SuggestedOriginalTicketID IN
          (SELECT ID FROM #TargetTicket);
    INSERT INTO @DeletedRows (TableName, RowsDeleted)
    VALUES (N'DuplicateReview', @@ROWCOUNT);

    DELETE assignmentRequest
    FROM dbo.TicketAssignmentRequest AS assignmentRequest
    INNER JOIN #TargetTicket AS target
        ON target.ID = assignmentRequest.TicketID;
    INSERT INTO @DeletedRows (TableName, RowsDeleted)
    VALUES (N'TicketAssignmentRequest', @@ROWCOUNT);

    DELETE workSession
    FROM dbo.TicketWorkSession AS workSession
    INNER JOIN #TargetTicket AS target
        ON target.ID = workSession.TicketID;
    INSERT INTO @DeletedRows (TableName, RowsDeleted)
    VALUES (N'TicketWorkSession', @@ROWCOUNT);

    DELETE history
    FROM dbo.TicketHistory AS history
    INNER JOIN #TargetTicket AS target
        ON target.ID = history.TicketID;
    INSERT INTO @DeletedRows (TableName, RowsDeleted)
    VALUES (N'TicketHistory', @@ROWCOUNT);

    DELETE attachment
    FROM dbo.TicketAttachment AS attachment
    INNER JOIN #TargetTicket AS target
        ON target.ID = attachment.TicketID;
    INSERT INTO @DeletedRows (TableName, RowsDeleted)
    VALUES (N'TicketAttachment', @@ROWCOUNT);

    DELETE comment
    FROM dbo.TicketComment AS comment
    INNER JOIN #TargetTicket AS target
        ON target.ID = comment.TicketID;
    INSERT INTO @DeletedRows (TableName, RowsDeleted)
    VALUES (N'TicketComment', @@ROWCOUNT);

    DELETE notification
    FROM dbo.UserNotification AS notification
    INNER JOIN #TargetTicket AS target
        ON target.TicketReferenceNumber = notification.TicketReferenceNumber;
    INSERT INTO @DeletedRows (TableName, RowsDeleted)
    VALUES (N'UserNotification', @@ROWCOUNT);

    DELETE activity
    FROM dbo.ActivityLog AS activity
    WHERE activity.EntityType = N'Ticket'
      AND EXISTS
      (
          SELECT 1
          FROM #TargetTicket AS target
          WHERE activity.EntityID = target.TicketReferenceNumber
             OR activity.EntityID = CONVERT(nvarchar(20), target.ID)
      );
    INSERT INTO @DeletedRows (TableName, RowsDeleted)
    VALUES (N'ActivityLog', @@ROWCOUNT);

    DELETE ticket
    FROM dbo.Ticket AS ticket
    INNER JOIN #TargetTicket AS target
        ON target.ID = ticket.ID;
    INSERT INTO @DeletedRows (TableName, RowsDeleted)
    VALUES (N'Ticket', @@ROWCOUNT);

    IF EXISTS
    (
        SELECT 1
        FROM dbo.Ticket
        WHERE TicketReferenceNumber LIKE N'RH-2026-9%'
    )
        THROW 51001,
            'Target ticket verification failed. The transaction was rolled back.',
            1;

    COMMIT TRANSACTION;

    SELECT TableName, RowsDeleted
    FROM @DeletedRows
    ORDER BY DeleteOrder;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
