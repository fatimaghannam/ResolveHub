namespace ResolveHub.Api.Entities;

public sealed class TicketComment
{
    public int ID { get; set; }
    public int TicketID { get; set; }
    public int AuthorUserAccountID { get; set; }
    public int? ParentCommentID { get; set; }
    public string Content { get; set; } = string.Empty;
    public CommentVisibility Visibility { get; set; } = CommentVisibility.Public;
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public bool IsEdited { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedDate { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public UserAccount AuthorUserAccount { get; set; } = null!;
    public TicketComment? ParentComment { get; set; }
    public ICollection<TicketComment> Replies { get; set; } = [];
    public ICollection<TicketCommentAttachment> Attachments { get; set; } = [];
}
