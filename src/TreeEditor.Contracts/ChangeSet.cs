namespace TreeEditor.Contracts;

public sealed record ChangeSet(
    IReadOnlyList<NewNode> Added,
    IReadOnlyList<ValueChange> Renamed,
    IReadOnlyList<Guid> Deleted,
    IReadOnlyList<Guid> Cached);

public sealed record NewNode(Guid Id, Guid ParentId, string Value);

public sealed record ValueChange(Guid Id, string Value);
