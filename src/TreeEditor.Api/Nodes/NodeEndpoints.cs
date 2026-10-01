using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using TreeEditor.Api.Data;
using TreeEditor.Contracts;

namespace TreeEditor.Api.Nodes;

public static class NodeEndpoints
{
    private static readonly Expression<Func<Node, NodeDto>> ToDto =
        n => new NodeDto(n.Id, n.ParentId, n.Value, n.IsDeleted, n.Children.Any());

    public static void MapNodeEndpoints(this IEndpointRouteBuilder app)
    {
        var nodes = app.MapGroup("/api/nodes");
        nodes.MapGet("", GetChildrenAsync);
        nodes.MapGet("/{id:guid}", GetNodeAsync);
        nodes.MapPost("/apply", ApplyAsync);

        app.MapPost("/api/reset", ResetAsync);
    }

    private static async Task<List<NodeDto>> GetChildrenAsync(Guid? parentId, TreeDbContext db, CancellationToken ct) =>
        await db.Nodes.Where(n => n.ParentId == parentId).Select(ToDto).ToListAsync(ct);

    private static async Task<Results<Ok<NodeDetails>, NotFound>> GetNodeAsync(Guid id, TreeDbContext db, CancellationToken ct)
    {
        var node = await db.Nodes.Where(n => n.Id == id).Select(ToDto).SingleOrDefaultAsync(ct);
        if (node is null)
            return TypedResults.NotFound();

        var ancestors = await db.Database.SqlQuery<Guid>($"""
            WITH RECURSIVE chain AS (
                SELECT "ParentId" FROM "Nodes" WHERE "Id" = {id}
                UNION ALL
                SELECT n."ParentId" FROM "Nodes" n JOIN chain c ON n."Id" = c."ParentId"
            )
            SELECT "ParentId" AS "Value" FROM chain WHERE "ParentId" IS NOT NULL
            """).ToListAsync(ct);

        return TypedResults.Ok(new NodeDetails(node, ancestors));
    }

    private static async Task<Results<Ok<List<NodeDto>>, ValidationProblem>> ApplyAsync(
        ChangeSet changes, TreeDbContext db, CancellationToken ct)
    {
        if (await ValidateAsync(changes, db, ct) is { } errors)
            return TypedResults.ValidationProblem(errors);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        db.Nodes.AddRange(changes.Added.Select(n => new Node { Id = n.Id, ParentId = n.ParentId, Value = n.Value }));
        await db.SaveChangesAsync(ct);
        await MarkDeletedAsync(db, [.. changes.Deleted], ct);

        var renamed = changes.Renamed.ToDictionary(c => c.Id, c => c.Value);
        var renamedIds = renamed.Keys.ToList();
        foreach (var node in await db.Nodes.Where(n => renamedIds.Contains(n.Id) && !n.IsDeleted).ToListAsync(ct))
            node.Value = renamed[node.Id];

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var cachedIds = changes.Cached.ToList();
        return TypedResults.Ok(await db.Nodes.Where(n => cachedIds.Contains(n.Id)).Select(ToDto).ToListAsync(ct));
    }

    private static async Task<Dictionary<string, string[]>?> ValidateAsync(ChangeSet changes, TreeDbContext db, CancellationToken ct)
    {
        var values = changes.Added.Select(n => n.Value).Concat(changes.Renamed.Select(c => c.Value));
        if (!values.All(v => !string.IsNullOrWhiteSpace(v) && v.Length <= NodeDto.MaxValueLength))
            return new() { ["value"] = [$"A value must be non-empty and at most {NodeDto.MaxValueLength} characters long."] };

        var newIds = changes.Added.Select(n => n.Id).ToHashSet();
        var parentIds = changes.Added.Select(n => n.ParentId).Where(id => !newIds.Contains(id)).Distinct().ToList();
        if (await db.Nodes.CountAsync(n => parentIds.Contains(n.Id) && !n.IsDeleted, ct) != parentIds.Count)
            return new() { ["parentId"] = ["A new node needs an existing parent that is not deleted."] };

        return null;
    }

    private static Task MarkDeletedAsync(TreeDbContext db, Guid[] ids, CancellationToken ct) =>
        db.Database.ExecuteSqlAsync($"""
            WITH RECURSIVE subtree AS (
                SELECT "Id" FROM "Nodes" WHERE "Id" = ANY({ids})
                UNION
                SELECT n."Id" FROM "Nodes" n JOIN subtree s ON n."ParentId" = s."Id"
            )
            UPDATE "Nodes" SET "IsDeleted" = TRUE
            WHERE "Id" IN (SELECT "Id" FROM subtree) AND NOT "IsDeleted"
            """, ct);

    private static async Task<NoContent> ResetAsync(TreeDbContext db, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Nodes.ExecuteDeleteAsync(ct);
        await SampleData.SeedAsync(db, ct);
        await transaction.CommitAsync(ct);
        return TypedResults.NoContent();
    }
}
