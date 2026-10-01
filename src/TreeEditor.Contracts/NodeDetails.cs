namespace TreeEditor.Contracts;

public sealed record NodeDetails(NodeDto Node, IReadOnlyList<Guid> Ancestors);
