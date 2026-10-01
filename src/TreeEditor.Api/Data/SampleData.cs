using Microsoft.EntityFrameworkCore;

namespace TreeEditor.Api.Data;

internal static class SampleData
{
    private static readonly Sample[] Tree =
    [
        Region("Europe",
            Region("Germany",
                Region("Bavaria",
                    Region("Munich", Region("Altstadt"), Region("Schwabing"), Region("Maxvorstadt")),
                    Region("Nuremberg")),
                Region("Hesse",
                    Region("Frankfurt", Region("Sachsenhausen"), Region("Westend")))),
            Region("Poland",
                Region("Masovia",
                    Region("Warsaw", Region("Mokotów"), Region("Praga"))),
                Region("Lesser Poland",
                    Region("Kraków")))),
        Region("Asia",
            Region("Japan",
                Region("Kantō",
                    Region("Tokyo", Region("Shibuya"), Region("Shinjuku")),
                    Region("Yokohama")),
                Region("Kansai",
                    Region("Osaka"),
                    Region("Kyoto"))))
    ];

    public static async Task SeedAsync(DbContext db, CancellationToken cancellationToken)
    {
        var nodes = db.Set<Node>();
        if (await nodes.AnyAsync(cancellationToken))
            return;

        nodes.AddRange(Tree.SelectMany(sample => Flatten(sample, null)));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<Node> Flatten(Sample sample, Guid? parentId)
    {
        var node = new Node { Id = Guid.CreateVersion7(), ParentId = parentId, Value = sample.Value };
        return sample.Children.SelectMany(child => Flatten(child, node.Id)).Prepend(node);
    }

    private static Sample Region(string value, params Sample[] children) => new(value, children);

    private sealed record Sample(string Value, Sample[] Children);
}
