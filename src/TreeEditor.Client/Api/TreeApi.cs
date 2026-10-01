using System.Net.Http.Json;
using TreeEditor.Contracts;

namespace TreeEditor.Client.Api;

internal sealed class TreeApi(HttpClient http)
{
    public async Task<IReadOnlyList<NodeDto>> GetChildrenAsync(Guid? parentId) =>
        await http.GetFromJsonAsync<NodeDto[]>(parentId is null ? "api/nodes" : $"api/nodes?parentId={parentId}") ?? [];

    public async Task<NodeDetails> GetNodeAsync(Guid id) =>
        await http.GetFromJsonAsync<NodeDetails>($"api/nodes/{id}")
        ?? throw new InvalidOperationException($"Node {id} was not returned.");

    public async Task<IReadOnlyList<NodeDto>> ApplyAsync(ChangeSet changes)
    {
        using var response = await http.PostAsJsonAsync("api/nodes/apply", changes);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NodeDto[]>() ?? [];
    }

    public async Task ResetAsync()
    {
        using var response = await http.PostAsync("api/reset", null);
        response.EnsureSuccessStatusCode();
    }
}
