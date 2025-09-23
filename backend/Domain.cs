using System;

public readonly record struct ProfileId(Guid Value)
{
    public static ProfileId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public sealed class Profile
{
    public ProfileId Id { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
}

public sealed class Item
{
    public string OwnerId { get; init; } = "";
    public required string Name { get; init; } = "";
}