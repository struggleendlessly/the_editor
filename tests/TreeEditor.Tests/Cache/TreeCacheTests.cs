using TreeEditor.Client.Cache;
using TreeEditor.Contracts;

namespace TreeEditor.Tests.Cache;

public sealed class TreeCacheTests
{
    private static readonly NodeDto Europe = Dto("Europe", null);
    private static readonly NodeDto Germany = Dto("Germany", Europe);
    private static readonly NodeDto Bavaria = Dto("Bavaria", Germany);
    private static readonly NodeDto Munich = Dto("Munich", Bavaria);
    private static readonly NodeDto Hesse = Dto("Hesse", Germany);
    private static readonly NodeDto[] Sample = [Europe, Germany, Bavaria, Munich, Hesse];

    private readonly TreeCache _cache = new();

    [Fact]
    public void Node_without_a_cached_parent_is_shown_as_root()
    {
        Load(Munich);

        Assert.Equal("Munich", Assert.Single(_cache.Roots).Value);
    }

    [Fact]
    public void Parent_loaded_later_adopts_cached_children()
    {
        Load(Munich);
        Load(Germany);
        Load(Bavaria);

        var germany = Assert.Single(_cache.Roots);
        var bavaria = Assert.Single(_cache.ChildrenOf(germany));
        Assert.Equal("Munich", Assert.Single(_cache.ChildrenOf(bavaria)).Value);
    }

    [Fact]
    public void Loading_a_cached_node_again_keeps_its_pending_changes()
    {
        Load(Munich);
        _cache.Rename(Find(Munich), "München");

        Load(Munich);

        Assert.Equal("München", Find(Munich).Value);
    }

    [Fact]
    public void Rename_marks_loaded_node_modified_and_keeps_new_node_added()
    {
        Load(Munich);
        var munich = Find(Munich);
        _cache.AddChild(munich, "Pasing");
        var pasing = Assert.Single(_cache.ChildrenOf(munich));

        _cache.Rename(munich, "München");
        _cache.Rename(pasing, "Pasing Nord");

        Assert.Equal(NodeState.Modified, munich.State);
        Assert.Equal(NodeState.Added, pasing.State);
        Assert.Equal("Pasing Nord", pasing.Value);
    }

    [Fact]
    public void Rename_to_the_same_value_is_not_a_change()
    {
        Load(Munich);

        _cache.Rename(Find(Munich), "Munich");

        Assert.False(_cache.HasChanges);
    }

    [Fact]
    public void Delete_marks_cached_subtree_and_discards_new_nodes()
    {
        Load(Germany);
        Load(Bavaria);
        Load(Hesse);
        var bavaria = Find(Bavaria);
        _cache.AddChild(bavaria, "Pasing");

        _cache.Delete(Find(Germany));

        Assert.All([Find(Germany), bavaria, Find(Hesse)], n => Assert.True(n.IsDeleted));
        Assert.Empty(_cache.ChildrenOf(bavaria));
        Assert.Equal(new[] { Germany.Id, Bavaria.Id, Hesse.Id }.Order(), _cache.GetChanges().Deleted.Order());
    }

    [Fact]
    public void Delete_reaches_descendants_whose_ancestors_are_not_cached()
    {
        Load(Germany);
        Load(Munich);

        _cache.Delete(Find(Germany));

        Assert.True(Find(Munich).IsDeleted);
        Assert.Equal(2, _cache.Roots.Count());
    }

    [Fact]
    public void Node_loaded_under_a_deleted_ancestor_arrives_deleted()
    {
        Load(Germany);
        _cache.Delete(Find(Germany));

        Load(Munich);

        Assert.True(Find(Munich).IsDeleted);
    }

    [Fact]
    public void Deleted_node_ignores_edits()
    {
        Load(Munich);
        var munich = Find(Munich);
        _cache.Delete(munich);

        _cache.Rename(munich, "München");
        _cache.AddChild(munich, "Pasing");

        Assert.Equal("Munich", munich.Value);
        Assert.Empty(_cache.ChildrenOf(munich));
    }

    [Fact]
    public void Change_set_describes_every_pending_operation()
    {
        Load(Germany);
        Load(Bavaria);
        Load(Hesse);
        _cache.Rename(Find(Bavaria), "Bayern");
        _cache.AddChild(Find(Bavaria), "Munich");
        _cache.Delete(Find(Hesse));

        var changes = _cache.GetChanges();

        var added = Assert.Single(changes.Added);
        Assert.Equal((Bavaria.Id, "Munich"), (added.ParentId, added.Value));
        Assert.Equal([new ValueChange(Bavaria.Id, "Bayern")], changes.Renamed);
        Assert.Equal([Hesse.Id], changes.Deleted);
        Assert.Equal(4, changes.Cached.Count);
    }

    [Fact]
    public void Refresh_applies_server_state_and_clears_pending_changes()
    {
        Load(Germany);
        Load(Munich);
        _cache.Rename(Find(Germany), "Deutschland");

        _cache.Refresh([Germany with { Value = "Deutschland" }, Munich with { IsDeleted = true }]);

        Assert.False(_cache.HasChanges);
        Assert.Equal("Deutschland", Find(Germany).Value);
        Assert.True(Find(Munich).IsDeleted);
    }

    private void Load(NodeDto node) => _cache.Load(new NodeDetails(node, [.. Ancestors(node)]));

    private CachedNode Find(NodeDto node) => All(_cache.Roots).Single(n => n.Id == node.Id);

    private IEnumerable<CachedNode> All(IEnumerable<CachedNode> nodes) =>
        nodes.SelectMany(n => All(_cache.ChildrenOf(n)).Prepend(n));

    private static IEnumerable<Guid> Ancestors(NodeDto node) =>
        node.ParentId is { } parentId ? Ancestors(Sample.Single(n => n.Id == parentId)).Append(parentId) : [];

    private static NodeDto Dto(string value, NodeDto? parent) =>
        new(Guid.CreateVersion7(), parent?.Id, value, IsDeleted: false, HasChildren: false);
}
