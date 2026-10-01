# Tree Editor

A web application for editing a large tree stored in a relational database through a local cache.
The left panel browses the database tree lazily; the right panel holds cached nodes, where values
can be edited, children added and subtrees deleted. Nothing reaches the database until **Apply**.

Built with .NET 10, Blazor WebAssembly, MudBlazor, EF Core and PostgreSQL.

## Running

Requires Docker.

```
docker compose up --build
```

Open http://localhost:5080. The database is created and filled with sample data on first start.
**Reset** in the top bar restores the sample data at any time. Data lives in a Docker volume;
`docker compose down -v` removes it.

### Local development

Requires the .NET 10 SDK.

```
docker compose up -d db
dotnet run --project src/TreeEditor.Api
```

The API project also serves the WebAssembly client, so the UI and the API share one origin.

### Tests

```
dotnet test
```

Unit tests cover the cache; integration tests run the API against a real PostgreSQL started with
Testcontainers, so Docker must be running. To use an existing database instead, set `TREE_TEST_DB` to
its connection string. The tests reset that database.

## Project layout

| Project | Contents |
|---|---|
| `TreeEditor.Contracts` | DTOs shared by the API and the client |
| `TreeEditor.Api` | Minimal API endpoints, EF Core model, migrations, sample data |
| `TreeEditor.Client` | Blazor WebAssembly UI and the cache (`Cache/TreeCache.cs`) |
| `TreeEditor.Tests` | Cache unit tests and API integration tests |

## Database schema

A single adjacency-list table:

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | Primary key |
| `ParentId` | `uuid`, nullable | References `Nodes.Id`, `ON DELETE RESTRICT`; indexed |
| `Value` | `varchar(200)` | |
| `IsDeleted` | `boolean` | Soft-delete flag |

The table is in third normal form: nothing derived is stored. Depth, paths and whether a node has
children are computed in queries. Since existing parent–child links never change, an adjacency list
needs no extra structure; a recursive CTE finds a node's ancestors and soft-deletes whole subtrees.

## Implementation notes

**The cache lives in the browser.** It is a flat dictionary of nodes. The hierarchy is computed from
it on render: a node whose parent is not cached is shown as a root. When the parent is loaded later,
its children move under it with no extra code. Each node is loaded together with the ids of its
ancestors, so the cache knows that two nodes are related even when the nodes between them were never
loaded: deleting a node marks every cached descendant, and a node loaded under a pending deletion
arrives deleted.

**Client-generated keys.** New nodes get a UUIDv7 in the browser, so Apply needs no temporary IDs or
ID mapping. Version 7 UUIDs are time-ordered and keep the primary key index compact.

**Apply** sends one change set (added, renamed, deleted, plus the IDs of every cached node) and runs it
in a single transaction: inserts, then a recursive soft delete of each deleted subtree, then value
changes for nodes that are still alive, so a deletion always wins over an edit. The server validates
values and requires every new node to have an existing, non-deleted parent. The response carries the
current state of every cached node, which the cache applies in place.

**Deletion** is a soft delete, so deleted nodes stay visible but read-only. It covers all descendants,
including ones that were never loaded. A node that was added and deleted before Apply is dropped from
the cache and never reaches the database.

**Scope.** The application assumes a single user. There is no optimistic concurrency control; the
server-side checks above are what keeps the data consistent if a stale client applies changes.
