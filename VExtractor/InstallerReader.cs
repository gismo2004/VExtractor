using System.Diagnostics;

namespace VExtractor;

/// <summary>
/// Takes the files the catalog is built from out of the service software's installer.
///
/// The installer is a Windows self-extracting archive: a small program followed by a 7-Zip
/// archive holding everything it would install. Nothing is installed and nothing inside it is
/// run; 7-Zip's console program reads the archive where it sits and copies out the files the
/// compiler needs. The archive is one solid stream, 425 MB packed and 3.2 GB unpacked, so a file
/// can only be reached by decompressing everything stored before it. 7-Zip stops once the
/// requested files are out: they sit a little past half-way, which takes about 15 s against 24 s
/// for the whole archive. The release archives carry that program beside this executable.
/// </summary>
public static class InstallerReader
{
    /// <summary>
    /// 7-Zip's console program: the bundled one beside this executable, otherwise one on the
    /// PATH, which is where a build from source finds it. The names differ per platform and per
    /// package (7za.exe bundled on Windows, 7zz in the official builds, 7z and 7za in most
    /// Linux packages).
    /// </summary>
    internal static string? FindSevenZip()
    {
        var candidates = OperatingSystem.IsWindows()
            ? new[] { "7za.exe", "7z.exe", "7zz.exe" }
            : new[] { "7zz", "7z", "7za" };

        var directories = new List<string> { AppContext.BaseDirectory };
        directories.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));

        foreach (var directory in directories)
            foreach (var name in candidates)
            {
                string full;
                try { full = Path.Combine(directory, name); }
                catch (ArgumentException) { break; }   // a malformed PATH entry
                if (File.Exists(full)) return full;
            }
        return null;
    }

    /// <summary>
    /// Copy every file whose name is in <paramref name="wanted"/> out of the installer into
    /// <paramref name="targetDir"/>, flat. Returns the names actually found. Throws when there
    /// is no 7-Zip, or when 7-Zip fails.
    /// </summary>
    public static List<string> Extract(string installerPath, IEnumerable<string> wanted, string targetDir)
    {
        var program = FindSevenZip();
        if (program == null)
        {
            var bundled = OperatingSystem.IsWindows() ? "7za.exe" : "7zz";
            throw new FileNotFoundException(
                $"7-Zip was not found: no {bundled} beside the program and none on the PATH. "
                + $"The release archive contains {bundled}: extract the whole archive into one folder, "
                + "or install 7-Zip.");
        }

        var names = new HashSet<string>(wanted, StringComparer.OrdinalIgnoreCase);
        Directory.CreateDirectory(targetDir);
        Console.WriteLine($"Reading {Path.GetFileName(installerPath)} with {program} ...");
        var start = new ProcessStartInfo(program)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        // e = extract flat, -y = no questions, -r = match the names in any directory (without
        // it a bare name only matches at the archive root and nothing comes out), -o = where.
        start.ArgumentList.Add("e");
        start.ArgumentList.Add("-y");
        start.ArgumentList.Add("-r");
        start.ArgumentList.Add("-o" + targetDir);
        start.ArgumentList.Add(installerPath);
        foreach (var name in names) start.ArgumentList.Add(name);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException($"could not start {program}");
        var output = process.StandardOutput.ReadToEnd();
        var errors = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"{Path.GetFileName(program)} failed with exit code {process.ExitCode}:\n{errors}\n{output}");

        var found = names.Where(n => File.Exists(Path.Combine(targetDir, n))).ToList();
        foreach (var name in found)
            Console.WriteLine($"  {name}  {new FileInfo(Path.Combine(targetDir, name)).Length / (1024 * 1024)} MB");
        return found;
    }
}
