using TreeEditor.Contracts;

namespace TreeEditor.Client.Cache;

public enum NodeState
{
    Unchanged,
    Added,
    Modified,
    Deleted
}

public sealed class CachedNode
{
    private CachedNode(Guid id, Guid? parentId, IReadOnlyList<Guid> ancestors, string value)
    {
        Id = id;
        ParentId = parentId;
        Ancestors = ancestors;
        Value = value;
    }

    public Guid Id { get; }

    public Guid? ParentId { get; }

    public IReadOnlyList<Guid> Ancestors { get; }

    public string Value { get; private set; }

    public bool IsDeleted { get; private set; }

    public NodeState State { get; private set; }

    public static CachedNode Loaded(NodeDetails details, bool ancestorDeleted)
    {
        var node = details.Node;
        return new(node.Id, node.ParentId, details.Ancestors, node.Value) { IsDeleted = node.IsDeleted || ancestorDeleted };
    }

    public static CachedNode Created(CachedNode parent, string value) =>
        new(Guid.CreateVersion7(), parent.Id, [.. parent.Ancestors, parent.Id], value) { State = NodeState.Added };

    public void Sync(NodeDto node)
    {
        Value = node.Value;
        IsDeleted = node.IsDeleted;
        State = NodeState.Unchanged;
    }

    public void Rename(string value)
    {
        if (IsDeleted || value == Value)
            return;

        Value = value;
        if (State == NodeState.Unchanged)
            State = NodeState.Modified;
    }

    public void MarkDeleted()
    {
        if (IsDeleted)
            return;

        IsDeleted = true;
        State = NodeState.Deleted;
    }
}
