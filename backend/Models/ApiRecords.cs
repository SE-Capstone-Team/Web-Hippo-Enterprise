namespace Backend.Models;

public sealed record BorrowRequest(string BorrowerId, DateTime? BorrowedOn, DateTime? DueAt);
public sealed record CreateRequestDto(string ItemId, string BorrowerId, DateTime? DueAt);
public sealed record RespondRequestDto(bool Accepted);
