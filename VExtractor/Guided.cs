using System.Runtime.InteropServices;
using VExtractor.Helper;
using VExtractor.Models.DataBase;

namespace VExtractor;

/// <summary>
/// What happens when the program is started without arguments: a guided run that finds the
/// controller definitions, prepares whatever is missing, asks which controller, and leaves
/// the catalog next to the program.
///
/// Sources are tried in this order, and the first that works is used:
///   1. the installed service software, on Windows, read in place;
///   2. files beside the program: a prepared source.db, or the definitions and texts;
///   3. the installer beside the program;
///   4. a path the user types.
/// Everything this run produces lands beside the program as well: the unpacked files, the
/// source database and the finished catalog. Nothing is written anywhere else.
/// </summary>
public static class Guided
{
    private const string Definitions = "DPDefinitions.xml";
    private const string SourceDb = "source.db";
    private const string DefaultLanguages = "de,en";

    public static int Run()
    {
        var home = WritableHome();
        Console.WriteLine();
        Console.WriteLine("VExtractor: builds the controller catalog for the OptoV integration.");
        Console.WriteLine($"Working in {home}");
        Console.WriteLine();
        try
        {
            return RunIn(home);
        }
        catch (OperationCanceledException err)
        {
            Console.WriteLine();
            Console.WriteLine($"Stopped: {err.Message}.");
            return 1;
        }
        catch (Exception err)
        {
            Console.WriteLine();
            Console.WriteLine($"Failed: {err.Message}");
            return 1;
        }
        finally
        {
            Pause();
        }
    }

    private static int RunIn(string home)
    {
        // ---- 1. Where do the definitions come from? ----
        var source = FindSource(home);
        if (source == null)
        {
            Console.WriteLine("Failed: no controller definitions available.");
            return 1;
        }

        // ---- 2. Which languages? Needed before unpacking an installer, since the text
        //         files come out of it per language. ----
        var languages = AskLanguages(source);

        // ---- 3. Make the source usable: unpack, load. ----
        if (!source.Ready(home, languages))
            return 1;

        // ---- 4. Which controller? ----
        using var vDb = new VDataBase();
        var controllers = SqliteExporter.Controllers(vDb, languages[0]);
        var ids = AskController(controllers);
        if (ids == null) return 1;

        // ---- 5. Build. ----
        var output = AskOutputPath(home, ids);
        if (output == null) return 1;
        Console.WriteLine();
        SqliteExporter.Export(vDb, output, ids, languages.ToList());
        Console.WriteLine();
        Console.WriteLine($"Done: {output}");
        Console.WriteLine("Add the OptoV integration in Home Assistant and upload this file when it asks.");
        return 0;
    }

    // ------------------------------------------------------------------ sources

    private abstract class Source
    {
        /// <summary>Languages that can be offered, or null when any the installer has.</summary>
        public abstract string[]? Languages { get; }

        /// <summary>Do whatever is needed so a VDataBase can be opened. False to give up.</summary>
        public abstract bool Ready(string home, string[] languages);
    }

    /// <summary>The service software installed on this Windows machine, read in place.</summary>
    private sealed class Installed : Source
    {
        private readonly string _database;
        private readonly string _texts;

        public Installed(string database, string texts) { _database = database; _texts = texts; }

        public override string[]? Languages => LanguagesIn(_texts);

        public override bool Ready(string home, string[] languages)
        {
            var missing = languages.Where(l => !File.Exists(Path.Combine(_texts, $"Textresource_{l}.xml"))).ToList();
            if (missing.Count > 0)
            {
                Console.WriteLine($"The installed software has no texts for: {string.Join(", ", missing)}");
                return false;
            }
            Environment.SetEnvironmentVariable("VEXTRACTOR_SOURCE_DB", null);
            Environment.SetEnvironmentVariable("VEXTRACTOR_SOURCE_DIR", _texts);
            Console.WriteLine($"Reading the installed service software: {_database}");
            return true;
        }
    }

    /// <summary>Definitions beside the program, already unpacked; the installer if present too.</summary>
    private sealed class Files : Source
    {
        private readonly string _dir;
        private readonly string? _installer;

        public Files(string dir, string? installer) { _dir = dir; _installer = installer; }

        public override string[]? Languages => _installer != null ? null : LanguagesIn(_dir);

        public override bool Ready(string home, string[] languages)
        {
            var wanted = languages.Select(l => $"Textresource_{l}.xml").ToList();
            var missing = wanted.Where(f => !File.Exists(Path.Combine(_dir, f))).ToList();
            if (missing.Count > 0)
            {
                if (_installer == null)
                {
                    Console.WriteLine($"No texts for: {string.Join(", ", missing.Select(f => f[13..^4]))}. "
                                      + "Put the installer beside the program to unpack them.");
                    return false;
                }
                Unpack(_installer, missing, _dir);
            }
            return Load(_dir);
        }
    }

    /// <summary>Only the installer, beside the program or as typed.</summary>
    private sealed class Installer : Source
    {
        private readonly string _path;

        public Installer(string path) { _path = path; }

        public override string[]? Languages => null;

        public override bool Ready(string home, string[] languages)
        {
            var wanted = new List<string> { Definitions };
            wanted.AddRange(languages.Select(l => $"Textresource_{l}.xml"));
            Unpack(_path, wanted, home);
            return Load(home);
        }
    }

    private static Source? FindSource(string home)
    {
        Console.WriteLine("Looking for controller definitions ...");

        // 1. Installed software, Windows only: the database file and the text files both
        //    live under the install directory, and the database is read through LocalDB.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var install = DataHelper.GetVInstallDir();
            if (!string.IsNullOrEmpty(install))
            {
                var database = Path.Combine(install, "ServiceTool", "Database", "ecnViessmann.mdf");
                var texts = Path.Combine(install, "ServiceTool", "Web", "XmlDocuments");
                if (File.Exists(database) && Directory.Exists(texts))
                {
                    Console.WriteLine($"  found the installed service software in {install}");
                    return new Installed(database, texts);
                }
            }
        }

        // 2. Files beside the program.
        var installerBeside = FindInstaller(home);
        if (File.Exists(Path.Combine(home, SourceDb)) || File.Exists(Path.Combine(home, Definitions)))
        {
            Console.WriteLine($"  found definitions beside the program");
            return new Files(home, installerBeside);
        }

        // 3. The installer beside the program.
        if (installerBeside != null)
        {
            Console.WriteLine($"  found the installer beside the program: {Path.GetFileName(installerBeside)}");
            return new Installer(installerBeside);
        }

        // 4. Ask.
        Console.WriteLine("  nothing found.");
        Console.WriteLine();
        Console.WriteLine("The catalog is built from the controller service software, Vitosoft 300 SID1.");
        Console.WriteLine("Its installer (Vitosoft300SID1_Setup.exe, a free 90-day demo from the manufacturer)");
        Console.WriteLine("does not need to be installed; it is only read. Put it next to this program,");
        Console.WriteLine("or type its path here.");
        while (true)
        {
            var typed = Ask("Path to the installer (Enter to quit)");
            if (string.IsNullOrWhiteSpace(typed)) return null;
            typed = typed.Trim().Trim('"');
            if (File.Exists(typed)) return new Installer(Path.GetFullPath(typed));
            if (Directory.Exists(typed) && File.Exists(Path.Combine(typed, Definitions)))
                return new Files(Path.GetFullPath(typed), FindInstaller(typed));
            Console.WriteLine("  not a file, and not a directory holding the definitions.");
        }
    }

    /// <summary>The installer, if one sits in the directory: a large .exe with "setup" in its name.</summary>
    private static string? FindInstaller(string dir)
    {
        // Measured through any symbolic link, so a link to the installer counts as the installer.
        static long SizeOf(string f)
        {
            try { return (File.ResolveLinkTarget(f, true) as FileInfo ?? new FileInfo(f)).Length; }
            catch (Exception) { return 0; }
        }
        try
        {
            return Directory.EnumerateFiles(dir, "*.exe")
                .Where(f => Path.GetFileName(f).Contains("setup", StringComparison.OrdinalIgnoreCase)
                            && SizeOf(f) > 100L * 1024 * 1024)
                .OrderByDescending(SizeOf)
                .FirstOrDefault();
        }
        catch (Exception) { return null; }
    }

    private static string[]? LanguagesIn(string dir)
    {
        if (!Directory.Exists(dir)) return null;
        var found = Directory.EnumerateFiles(dir, "Textresource_*.xml")
            .Select(f => Path.GetFileNameWithoutExtension(f)["Textresource_".Length..])
            .OrderBy(l => l, StringComparer.Ordinal)
            .ToArray();
        return found.Length > 0 ? found : null;
    }

    // ------------------------------------------------------------------ steps

    private static void Unpack(string installer, List<string> wanted, string into)
    {
        Console.WriteLine();
        Console.WriteLine("Unpacking the installer, about 15 seconds ...");
        var got = InstallerReader.Extract(installer, wanted, into);
        var missing = wanted.Where(w => !got.Contains(w, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missing.Count > 0)
            throw new InvalidDataException($"not in this installer: {string.Join(", ", missing)}");
    }

    /// <summary>Turn the definitions into source.db unless an up-to-date one is already there.</summary>
    private static bool Load(string dir)
    {
        var xml = Path.Combine(dir, Definitions);
        var db = Path.Combine(dir, SourceDb);
        Environment.SetEnvironmentVariable("VEXTRACTOR_SOURCE_DB", db);
        Environment.SetEnvironmentVariable("VEXTRACTOR_SOURCE_DIR", dir);
        if (File.Exists(db) && (!File.Exists(xml) || File.GetLastWriteTimeUtc(xml) <= File.GetLastWriteTimeUtc(db)))
        {
            Console.WriteLine($"Using the prepared definitions: {db}");
            return true;
        }
        if (!File.Exists(xml))
        {
            Console.WriteLine($"Neither {Definitions} nor {SourceDb} is in {dir}.");
            return false;
        }
        Console.WriteLine();
        Console.WriteLine("Loading the definitions (about twenty seconds, once) ...");
        SourceLoader.Load(xml, db);
        return true;
    }

    private static string[] AskLanguages(Source source)
    {
        var available = source.Languages;
        Console.WriteLine();
        if (available != null)
            Console.WriteLine($"Languages available: {string.Join(", ", available)}");
        else
            Console.WriteLine("Languages the catalog should carry, as two-letter codes. The installer has de, en,");
        Console.WriteLine("fr, it, es, nl, pl, da, sv, cs, ru, tr, hu, hr, no, sk, ro and lt. Only these are offered");
        Console.WriteLine("in the integration afterwards, and each adds about 4 MB to a full catalog.");
        while (true)
        {
            var typed = Ask($"Languages [{DefaultLanguages}]");
            var chosen = (string.IsNullOrWhiteSpace(typed) ? DefaultLanguages : typed)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(l => l.ToLowerInvariant()).Distinct().ToArray();
            if (chosen.All(l => l.Length == 2 && l.All(char.IsLetter))) return chosen;
            Console.WriteLine("  two-letter codes, comma-separated, please.");
        }
    }

    private static List<string>? AskController(List<SqliteExporter.ControllerInfo> controllers)
    {
        Console.WriteLine();
        Console.WriteLine($"{controllers.Count} controllers are described. Which one is yours?");
        Console.WriteLine("Type its system id if you know it, or part of the name on the unit to search,");
        Console.WriteLine("or 'all' for every controller (a much larger catalog that needs no decision).");
        while (true)
        {
            var typed = Ask("Controller (Enter to quit)");
            if (string.IsNullOrWhiteSpace(typed)) return null;
            typed = typed.Trim();

            if (typed.Equals("all", StringComparison.OrdinalIgnoreCase))
                return new List<string> { "all" };

            var ids = typed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(i => i.ToUpperInvariant()).ToList();
            if (ids.All(i => i.Length == 4 && i.All(Uri.IsHexDigit)))
            {
                var unknown = ids.Where(i => controllers.All(c => c.SystemId != i)).ToList();
                if (unknown.Count == 0) return ids;
                Console.WriteLine($"  no controller has the system id {string.Join(", ", unknown)}.");
                continue;
            }

            var hits = controllers
                .Where(c => c.Model.Contains(typed, StringComparison.OrdinalIgnoreCase)
                            || c.Description.Contains(typed, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (hits.Count == 0)
            {
                Console.WriteLine("  nothing matches. Try a shorter part of the name, for example 'Vitocal' or 'WO1A'.");
                continue;
            }
            Console.WriteLine();
            SqliteExporter.PrintControllers(hits);
            Console.WriteLine();
            Console.WriteLine("Type the system id from the list.");
        }
    }

    private static string? AskOutputPath(string home, List<string> ids)
    {
        var name = ids.Count == 1 && ids[0] != "all" ? $"catalog-{ids[0]}.db" : "catalog.db";
        var path = Path.Combine(home, name);
        if (!File.Exists(path)) return path;
        var answer = Ask($"{name} exists beside the program. Overwrite? [y/N]");
        if (answer.Trim().Equals("y", StringComparison.OrdinalIgnoreCase)) return path;
        var other = Ask("Other file name (Enter to quit)");
        if (string.IsNullOrWhiteSpace(other)) return null;
        other = other.Trim();
        if (!other.EndsWith(".db", StringComparison.OrdinalIgnoreCase)) other += ".db";
        return Path.Combine(home, other);
    }

    // ------------------------------------------------------------------ console

    /// <summary>
    /// Beside the program when that can be written to, otherwise the current directory. Or
    /// wherever VEXTRACTOR_HOME points: in the container the program sits in its own image
    /// and the user's folder is mounted elsewhere, so "beside the program" is the wrong place.
    /// </summary>
    private static string WritableHome()
    {
        var forced = Environment.GetEnvironmentVariable("VEXTRACTOR_HOME");
        if (!string.IsNullOrWhiteSpace(forced) && Directory.Exists(forced))
            return Path.GetFullPath(forced);
        var beside = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        try
        {
            var probe = Path.Combine(beside, $".write-test-{Environment.ProcessId}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return beside;
        }
        catch (Exception)
        {
            return Directory.GetCurrentDirectory();
        }
    }

    private static string Ask(string prompt)
    {
        Console.Write($"{prompt}: ");
        return Console.ReadLine() ?? "";
    }

    /// <summary>
    /// A console opened by double-clicking closes the moment the program ends, taking the
    /// last message with it. Wait for a key when someone is actually sitting at the keyboard.
    /// </summary>
    private static void Pause()
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected) return;
        // The launcher keeps its window open itself; two prompts in a row would be one too many.
        if (Environment.GetEnvironmentVariable("VEXTRACTOR_LAUNCHER") == "1") return;
        Console.WriteLine();
        Console.Write("Press Enter to close.");
        Console.ReadLine();
    }
}
