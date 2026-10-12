// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.IO;

namespace PsGuiBuilder;

public static class CommandTreeLocator
{
    public static string Find(IReadOnlyList<string> args)
    {
        var flag = -1;
        for (var index = 0; index < args.Count; index++)
        {
            if (string.Equals(args[index], "--tree", StringComparison.OrdinalIgnoreCase))
            {
                flag = index;
                break;
            }
        }

        if (flag >= 0)
        {
            if (flag + 1 >= args.Count)
                throw new ArgumentException("Pass a file path after --tree.");

            var requested = Path.GetFullPath(args[flag + 1]);
            if (!File.Exists(requested))
                throw new FileNotFoundException("The command tree file was not found.", requested);

            return requested;
        }

        var besideApp = Path.Combine(AppContext.BaseDirectory, "command-tree.json");
        if (File.Exists(besideApp))
            return besideApp;

        var current = Path.Combine(Directory.GetCurrentDirectory(), "command-tree.json");
        if (File.Exists(current))
            return current;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "command-tree.json");
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException("command-tree.json was not found. Pass --tree <path>.");
    }
}
