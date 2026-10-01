using TreeEditor.Contracts;

namespace TreeEditor.Client.Cache;

public sealed class TreeCache
{
    public static readonly StringComparer ByValue = StringComparer.CurrentCultureIgnoreCase;

    private readonly Dictionary<Guid, CachedNode> _nodes = [];
    private ILookup<Guid?, CachedNode>? _children;

    public event Action? Changed;

    public bool IsEmpty => _nodes.Count == 0;

    public bool HasChanges => _nodes.Values.Any(n => n.State != NodeState.Unchanged);

    public IEnumerable<CachedNode> Roots =>
        _nodes.Values.Where(n => !IsCached(n.ParentId)).OrderBy(n => n.Value, ByValue);

    private ILookup<Guid?, CachedNode> Children =>
        _children ??= _nodes.Values.OrderBy(n => n.Value, ByValue).ToLookup(n => n.ParentId);

    public IEnumerable<CachedNode> ChildrenOf(CachedNode node) => Children[node.Id];

    public bool Contains(Guid id) => _nodes.ContainsKey(id);

    public void Load(NodeDetails details)
    {
        var ancestorDeleted = details.Ancestors.Any(id => _nodes.TryGetValue(id, out var ancestor) && ancestor.IsDeleted);
        if (_nodes.TryAdd(details.Node.Id, CachedNode.Loaded(details, ancestorDeleted)))
            OnChanged();
    }

    public void AddChild(CachedNode parent, string value)
    {
        if (parent.IsDeleted)
            return;

        var child = CachedNode.Created(parent, value);
        _nodes.Add(child.Id, child);
        OnChanged();
    }

    public void Rename(CachedNode node, string value)
    {
        node.Rename(value);
        OnChanged();
    }

    public void Delete(CachedNode node)
    {
        foreach (var item in _nodes.Values.Where(n => n.Id == node.Id || n.Ancestors.Contains(node.Id)).ToList())
        {
            if (item.State == NodeState.Added)
                _nodes.Remove(item.Id);
            else
                item.MarkDeleted();
        }

        OnChanged();
    }

    public ChangeSet GetChanges() => new(
        [.. InState(NodeState.Added).Select(n => new NewNode(n.Id, n.ParentId!.Value, n.Value))],
        [.. InState(NodeState.Modified).Select(n => new ValueChange(n.Id, n.Value))],
        [.. InState(NodeState.Deleted).Select(n => n.Id)],
        [.. _nodes.Keys]);

    public void Refresh(IEnumerable<NodeDto> nodes)
    {
        foreach (var node in nodes)
            _nodes[node.Id].Sync(node);

        OnChanged();
    }

    public void Clear()
    {
        _nodes.Clear();
        OnChanged();
    }

    private IEnumerable<CachedNode> InState(NodeState state) => _nodes.Values.Where(n => n.State == state);

    private bool IsCached(Guid? id) => id is { } value && _nodes.ContainsKey(value);

    private void OnChanged()
    {
        _children = null;
        Changed?.Invoke();
    }
}
