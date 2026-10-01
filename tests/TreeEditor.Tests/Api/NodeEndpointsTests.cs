using System.Net;
using System.Net.Http.Json;
using TreeEditor.Contracts;

namespace TreeEditor.Tests.Api;

public sealed class NodeEndpointsTests(TreeApiFactory factory) : IClassFixture<TreeApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public async ValueTask InitializeAsync() => (await _client.PostAsync("/api/reset", null)).EnsureSuccessStatusCode();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Tree_is_served_level_by_level()
    {
        var roots = await ChildrenAsync(null);
        var europe = roots.Single(n => n.Value == "Europe");
        var countries = await ChildrenAsync(europe.Id);

        Assert.Equal(["Asia", "Europe"], roots.Select(n => n.Value).Order());
        Assert.Equal(["Germany", "Poland"], countries.Select(n => n.Value).Order());
        Assert.All(countries, n => Assert.True(n.HasChildren));
    }

    [Fact]
    public async Task Sample_data_is_at_least_four_levels_deep()
    {
        var leaf = await FindAsync("Europe", "Germany", "Bavaria", "Munich", "Altstadt");

        Assert.False(leaf.HasChildren);
    }

    [Fact]
    public async Task Loaded_node_carries_its_ancestors()
    {
        var path = await PathAsync("Europe", "Germany", "Hesse", "Frankfurt", "Westend");

        var details = await _client.GetFromJsonAsync<NodeDetails>($"/api/nodes/{path[^1].Id}");

        Assert.Equal(path[^1], details!.Node);
        Assert.Equal(path[..^1].Select(n => n.Id).Order(), details.Ancestors.Order());
    }

    [Fact]
    public async Task Apply_inserts_and_renames_nodes_and_returns_their_state()
    {
        var munich = await FindAsync("Europe", "Germany", "Bavaria", "Munich");
        var pasing = new NewNode(Guid.CreateVersion7(), munich.Id, "Pasing");

        var state = await ApplyAsync(new([pasing], [new(munich.Id, "München")], [], [munich.Id, pasing.Id]));

        Assert.Equal("München", state.Single(n => n.Id == munich.Id).Value);
        Assert.Contains(state, n => n.Id == pasing.Id && n.ParentId == munich.Id);
        Assert.Equal(pasing.Id, Assert.Single(await ChildrenAsync(munich.Id), n => n.Value == "Pasing").Id);
    }

    [Fact]
    public async Task Delete_cascades_to_descendants_that_were_never_loaded()
    {
        var germany = await FindAsync("Europe", "Germany");
        var westend = await FindAsync("Europe", "Germany", "Hesse", "Frankfurt", "Westend");

        var state = await ApplyAsync(new([], [], [germany.Id], [germany.Id, westend.Id]));

        Assert.All(state, n => Assert.True(n.IsDeleted));
        Assert.True((await FindAsync("Europe", "Germany", "Bavaria", "Munich", "Altstadt")).IsDeleted);
        Assert.False((await FindAsync("Europe", "Poland")).IsDeleted);
    }

    [Fact]
    public async Task Deletion_wins_over_a_rename_in_the_same_change_set()
    {
        var japan = await FindAsync("Asia", "Japan");
        var osaka = await FindAsync("Asia", "Japan", "Kansai", "Osaka");

        var state = await ApplyAsync(new([], [new(osaka.Id, "Ōsaka")], [japan.Id], [osaka.Id]));

        var result = Assert.Single(state);
        Assert.True(result.IsDeleted);
        Assert.Equal("Osaka", result.Value);
    }

    [Fact]
    public async Task New_node_under_a_parent_deleted_in_the_same_change_set_is_deleted()
    {
        var japan = await FindAsync("Asia", "Japan");
        var sapporo = new NewNode(Guid.CreateVersion7(), japan.Id, "Sapporo");

        var state = await ApplyAsync(new([sapporo], [], [japan.Id], [sapporo.Id]));

        Assert.True(Assert.Single(state).IsDeleted);
    }

    [Fact]
    public async Task Rename_of_a_deleted_node_is_ignored()
    {
        var osaka = await FindAsync("Asia", "Japan", "Kansai", "Osaka");
        await ApplyAsync(new([], [], [osaka.Id], []));

        var state = await ApplyAsync(new([], [new(osaka.Id, "Ōsaka")], [], [osaka.Id]));

        Assert.Equal("Osaka", Assert.Single(state).Value);
    }

    [Fact]
    public async Task New_node_under_a_deleted_parent_is_rejected()
    {
        var japan = await FindAsync("Asia", "Japan");
        await ApplyAsync(new([], [], [japan.Id], []));

        var response = await PostApplyAsync(new([new(Guid.CreateVersion7(), japan.Id, "Sapporo")], [], [], []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task New_node_under_an_unknown_parent_is_rejected()
    {
        var response = await PostApplyAsync(new([new(Guid.CreateVersion7(), Guid.CreateVersion7(), "Nowhere")], [], [], []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_value_is_rejected(string value)
    {
        var tokyo = await FindAsync("Asia", "Japan", "Kantō", "Tokyo");

        var response = await PostApplyAsync(new([], [new(tokyo.Id, value)], [], []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Too_long_value_is_rejected()
    {
        var tokyo = await FindAsync("Asia", "Japan", "Kantō", "Tokyo");
        var value = new string('x', NodeDto.MaxValueLength + 1);

        var response = await PostApplyAsync(new([], [new(tokyo.Id, value)], [], []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reset_restores_the_sample_data()
    {
        var europe = await FindAsync("Europe");
        await ApplyAsync(new([], [new(europe.Id, "Old World")], [europe.Id], []));

        (await _client.PostAsync("/api/reset", null)).EnsureSuccessStatusCode();

        Assert.False((await FindAsync("Europe")).IsDeleted);
    }

    [Fact]
    public async Task Unknown_node_is_not_found()
    {
        var response = await _client.GetAsync($"/api/nodes/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<NodeDto[]> ChildrenAsync(Guid? parentId) =>
        await _client.GetFromJsonAsync<NodeDto[]>(parentId is null ? "/api/nodes" : $"/api/nodes?parentId={parentId}") ?? [];

    private async Task<NodeDto> FindAsync(params string[] path) => (await PathAsync(path))[^1];

    private async Task<NodeDto[]> PathAsync(params string[] path)
    {
        var nodes = new List<NodeDto>();
        foreach (var value in path)
            nodes.Add((await ChildrenAsync(nodes.LastOrDefault()?.Id)).Single(n => n.Value == value));

        return [.. nodes];
    }

    private Task<HttpResponseMessage> PostApplyAsync(ChangeSet changes) => _client.PostAsJsonAsync("/api/nodes/apply", changes);

    private async Task<NodeDto[]> ApplyAsync(ChangeSet changes)
    {
        var response = await PostApplyAsync(changes);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NodeDto[]>() ?? [];
    }
}
