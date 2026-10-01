namespace TreeEditor.Api.Data;

public sealed class Node
{
    public Guid Id { get; init; }

    public Guid? ParentId { get; init; }

    public required string Value { get; set; }

    public bool IsDeleted { get; set; }

    public List<Node> Children { get; } = [];
}
