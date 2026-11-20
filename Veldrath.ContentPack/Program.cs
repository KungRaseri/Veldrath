using RealmEngine.Data.Authoring.ContentPacks;

namespace Veldrath.ContentPack;

/// <summary>
/// Entry point for the content-pack tool: <c>validate</c> runs the content validators over a pack directory, and
/// <c>import</c> upserts one into the content catalog by slug.
/// </summary>
/// <remarks>
/// Everything the tool does lives in <see cref="ContentPackConsole"/> so the exit codes are covered by tests; this
/// class only wires the process streams.
/// </remarks>
internal static class Program
{
    private static int Main(string[] args) =>
        ContentPackConsole.Run(args, Console.Out, Console.Error);
}
