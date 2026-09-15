using System.ComponentModel;
using System.Data;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Win32;

namespace VExtractor.Helper;

public class DataHelper
{
    private const string VdbName = "ecnViessmann.mdf";

    public static string GetVInstallDir()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return string.Empty;

        // Locates a local service-tool installation so the parameter database can be read
        // in place. Windows only; every other platform falls back to VExtractor/database/.
        var registryPath = @"SOFTWARE\Avantgarde\Setup\Viessmann Vitosoft 300 SID1";
        using var registryKey = Registry.LocalMachine.OpenSubKey(registryPath);

        return registryKey?.GetValue("InstallDir")?.ToString() ?? string.Empty;
    }

    public static string GetConnectionString()
    {
        var envConn = Environment.GetEnvironmentVariable("VEXTRACTOR_CONNECTION_STRING");
        if (!string.IsNullOrEmpty(envConn))
            return envConn;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var dbPath = GetDatabaseFilePath();
                return $"Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename={dbPath};Integrated Security=True;Connect Timeout=30;Encrypt=True";
            }
            catch
            {
                // Fall back to standard connection string
            }
        }

        // Local extraction container only -- never a shared or production server. Override with
        // VEXTRACTOR_CONNSTR (or VEXTRACTOR_SA_PASSWORD) rather than editing this default.
        var fromEnv = Environment.GetEnvironmentVariable("VEXTRACTOR_CONNSTR");
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;

        var password = Environment.GetEnvironmentVariable("VEXTRACTOR_SA_PASSWORD") ?? "VExtractor@2026!";
        return $"Server=localhost,1433;Database=ecnViessmann;User Id=sa;Password={password};TrustServerCertificate=True;";
    }

    public static string GetResultDataPath() => Path.Combine(Directory.GetCurrentDirectory(), "Data");

    public static string GetDatabaseFilePath()
    {
        var candidates = new[]
        {
            Path.Combine(GetResultDataPath(), VdbName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database", VdbName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, VdbName),
            Path.Combine(Directory.GetCurrentDirectory(), "database", VdbName),
            Path.Combine(Directory.GetCurrentDirectory(), "work", "database", VdbName),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "work", "database", VdbName),
            Path.Combine(Directory.GetCurrentDirectory(), VdbName),
            Path.Combine(GetVInstallDir(), "ServiceTool\\Database", VdbName)
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        throw new Exception($"Can't find {VdbName}");
    }

    public static string GetTranslationFilePath(string fileName)
    {
        var sourceDir = Environment.GetEnvironmentVariable("VEXTRACTOR_SOURCE_DIR");
        var candidates = new[]
        {
            Path.Combine(string.IsNullOrWhiteSpace(sourceDir) ? GetResultDataPath() : sourceDir, fileName),
            Path.Combine(GetResultDataPath(), fileName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database", fileName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "work", "database", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "work", "database", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "database", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), fileName),
            Path.Combine(GetVInstallDir(), "ServiceTool\\Web\\XmlDocuments", fileName)
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        throw new Exception($"Can't find {fileName}");
    }

    public static DataTable ConvertToDataTable<T>(IList<T> data)
    {
        var properties = TypeDescriptor.GetProperties(typeof(T));
        var ignoredList = new List<string>();
        var dataTable = new DataTable();
        foreach (PropertyDescriptor propertyDescriptor in properties)
            if (propertyDescriptor.SerializationVisibility == DesignerSerializationVisibility.Hidden)
            {
                ignoredList.Add(propertyDescriptor.Name);
            }
            else
            {
                var columns = dataTable.Columns;
                var name = propertyDescriptor.Name;
                var type = Nullable.GetUnderlyingType(propertyDescriptor.PropertyType) ??
                           propertyDescriptor.PropertyType;
                columns.Add(name, type);
            }

        foreach (var component in data)
        {
            var row = dataTable.NewRow();
            foreach (var propertyDescriptor in properties.Cast<PropertyDescriptor>()
                         .Where(prop => !ignoredList.Contains(prop.Name)))
                row[propertyDescriptor.Name] = propertyDescriptor.GetValue(component) ?? DBNull.Value;
            dataTable.Rows.Add(row);
        }

        return dataTable;
    }

    public static object Deserialize(byte[] bytes)
    {
        using var serializationStream = new MemoryStream(bytes);
#pragma warning disable SYSLIB0011
        //that's how data is stored in database...
        return new BinaryFormatter().Deserialize(serializationStream);
#pragma warning restore SYSLIB0011
    }

    public static byte[] Serialize(object obj)
    {
        using var serializationStream = new MemoryStream();
#pragma warning disable SYSLIB0011
        new BinaryFormatter().Serialize(serializationStream, obj);
#pragma warning restore SYSLIB0011
        return serializationStream.ToArray();
    }
}