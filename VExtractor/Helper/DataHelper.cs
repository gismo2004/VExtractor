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

        var registryPath = @"SOFTWARE\Avantgarde\Setup\Viessmann Vitosoft 300 SID1";
        using var registryKey = Registry.LocalMachine.OpenSubKey(registryPath);

        return registryKey?.GetValue("InstallDir")?.ToString() ?? string.Empty;
    }

    public static string GetResultDataPath()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
    }

    public static string GetDatabaseFilePath()
    {
        //first priority is Data Folder
        if (File.Exists(Path.Combine(GetResultDataPath(), VdbName)))
            return Path.Combine(GetResultDataPath(), VdbName);

        if (File.Exists(Path.Combine(GetVInstallDir(), "ServiceTool\\Database", VdbName)))
            return Path.Combine(GetVInstallDir(), "ServiceTool\\Database", VdbName);

        throw new Exception($"Can't find {VdbName}");
    }

    public static string GetTranslationFilePath(string fileName)
    {
        //first priority is Data Folder
        if (File.Exists(Path.Combine(GetResultDataPath(), fileName)))
            return Path.Combine(GetResultDataPath(), fileName);

        if (File.Exists(Path.Combine(GetVInstallDir(), "ServiceTool\\Web\\XmlDocuments", fileName)))
            return Path.Combine(GetVInstallDir(), "ServiceTool\\Web\\XmlDocuments", fileName);

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