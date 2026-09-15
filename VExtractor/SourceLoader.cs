using System.Data;
using System.Globalization;
using System.Xml;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using VExtractor.Models.DataBase;

namespace VExtractor;

/// <summary>
/// Turns the controller definitions shipped as a serialized dataset into a SQLite source
/// database the exporter can read, so the whole build runs from the installer's files on
/// any platform, with no database server and without the software ever being installed.
///
/// The file is a .NET DataSet with its schema inline and one table element per table, named
/// exactly as the tables the data model expects. Every table is written as-is: one SQLite
/// table per DataTable, columns named as in the file, typed by affinity from the .NET type.
/// </summary>
public static class SourceLoader
{
    public static void Load(string xmlPath, string sqlitePath)
    {
        Console.WriteLine($"Reading definitions from {xmlPath} ...");
        var dataSet = new DataSet { EnforceConstraints = false };
        using (var reader = XmlReader.Create(xmlPath, new XmlReaderSettings { IgnoreWhitespace = true }))
        {
            // The file is a wrapper around the dataset, which in turn holds its schema first
            // and its rows second, as a diffgram. The two parts are read separately: the
            // schema defines the tables, the diffgram fills them.
            if (!reader.ReadToDescendant("ECNDataSet"))
                throw new InvalidDataException("No ECNDataSet element in the definition file");
            if (!reader.ReadToDescendant("schema", "http://www.w3.org/2001/XMLSchema"))
                throw new InvalidDataException("No inline schema in the definition file");
            using (var schema = reader.ReadSubtree())
                dataSet.ReadXmlSchema(schema);
            reader.Skip();
            while (reader.NodeType != XmlNodeType.Element && reader.Read()) { }
            if (reader.LocalName != "diffgram")
                throw new InvalidDataException($"Expected a diffgram after the schema, found {reader.LocalName}");
            using (var rows = reader.ReadSubtree())
                dataSet.ReadXml(rows, XmlReadMode.DiffGram);
        }
        Console.WriteLine($"  {dataSet.Tables.Count} tables, {dataSet.Tables.Cast<DataTable>().Sum(t => t.Rows.Count)} rows");

        if (File.Exists(sqlitePath)) File.Delete(sqlitePath);
        using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        connection.Open();
        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode=OFF; PRAGMA synchronous=OFF;";
            pragma.ExecuteNonQuery();
        }

        foreach (DataTable table in dataSet.Tables)
        {
            if (table.Rows.Count == 0) continue;
            using var transaction = connection.BeginTransaction();
            var columns = table.Columns.Cast<DataColumn>().ToList();
            using (var create = connection.CreateCommand())
            {
                create.Transaction = transaction;
                create.CommandText =
                    $"CREATE TABLE \"{table.TableName}\" (" +
                    string.Join(", ", columns.Select(c => $"\"{c.ColumnName}\" {Affinity(c.DataType)}")) + ")";
                create.ExecuteNonQuery();
            }
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                $"INSERT INTO \"{table.TableName}\" VALUES (" +
                string.Join(", ", columns.Select((_, i) => $"@p{i}")) + ")";
            var parameters = columns.Select((_, i) =>
            {
                var p = insert.CreateParameter();
                p.ParameterName = $"@p{i}";
                insert.Parameters.Add(p);
                return p;
            }).ToArray();

            foreach (DataRow row in table.Rows)
            {
                for (var i = 0; i < columns.Count; i++)
                    parameters[i].Value = Value(row[i]);
                insert.ExecuteNonQuery();
            }
            transaction.Commit();
            Console.WriteLine($"  {table.TableName}: {table.Rows.Count} rows");
        }
        AlignWithModel(connection, sqlitePath);
        Console.WriteLine($"Source database written to {sqlitePath}");
    }

    /// <summary>
    /// Make every table and column the data model maps exist, empty where the definition
    /// file has nothing for it. The file and the model come from different releases of the
    /// same schema and drift by the odd column; a query selecting a column that is not there
    /// would otherwise fail, whereas a NULL is what the model expects for an absent value.
    /// </summary>
    private static void AlignWithModel(SqliteConnection connection, string sqlitePath)
    {
        Environment.SetEnvironmentVariable("VEXTRACTOR_SOURCE_DB", sqlitePath);
        using var context = new VDataBase();
        var added = 0;
        foreach (var entity in context.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table == null) continue;  // views are not loaded and no longer queried
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            var wanted = entity.GetProperties()
                .Select(p => (Name: p.GetColumnName(store), Type: p.ClrType, Nullable: p.IsNullable))
                .Where(c => c.Name != null)
                .ToList();
            if (wanted.Count == 0) continue;

            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var info = connection.CreateCommand())
            {
                info.CommandText = $"PRAGMA table_info(\"{table}\")";
                using var reader = info.ExecuteReader();
                while (reader.Read()) existing.Add(reader.GetString(1));
            }
            using var command = connection.CreateCommand();
            if (existing.Count == 0)
            {
                command.CommandText = $"CREATE TABLE \"{table}\" (" +
                    string.Join(", ", wanted.Select(c => $"\"{c.Name}\" {ColumnSpec(c.Type, c.Nullable)}")) + ")";
                command.ExecuteNonQuery();
                added += wanted.Count;
                continue;
            }
            foreach (var column in wanted.Where(c => !existing.Contains(c.Name!)))
            {
                // A column the model reads as a plain value type cannot be NULL, so an absent
                // one gets the type's own default rather than nothing.
                command.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column.Name}\" " +
                    ColumnSpec(column.Type, column.Nullable);
                command.ExecuteNonQuery();
                added++;
            }
        }
        Console.WriteLine($"  aligned with the data model: {added} columns added as empty");
    }

    private static string ColumnSpec(Type type, bool nullable)
    {
        var clr = Nullable.GetUnderlyingType(type) ?? type;
        var affinity = Affinity(clr);
        if (nullable) return affinity;
        var fallback = clr == typeof(string) ? "''"
            : clr == typeof(byte[]) ? "X''"
            : clr == typeof(DateTime) ? "'0001-01-01 00:00:00'"
            : "0";
        return $"{affinity} NOT NULL DEFAULT {fallback}";
    }

    private static string Affinity(Type type)
    {
        if (type == typeof(bool) || type == typeof(byte) || type == typeof(short)
            || type == typeof(int) || type == typeof(long)) return "INTEGER";
        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal)) return "REAL";
        if (type == typeof(byte[])) return "BLOB";
        return "TEXT";
    }

    private static object Value(object value) => value switch
    {
        DBNull => DBNull.Value,
        bool b => b ? 1 : 0,
        // The same text form the SQLite provider of the data model reads back as DateTime.
        DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
        _ => value,
    };
}
