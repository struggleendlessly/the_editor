namespace TreeEditor.Contracts;

public sealed record NodeDto(Guid Id, Guid? ParentId, string Value, bool IsDeleted, bool HasChildren)
{
    public const int MaxValueLength = 200;
}
