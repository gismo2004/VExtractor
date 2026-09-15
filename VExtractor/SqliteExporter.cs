using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VExtractor.Helper;
using VExtractor.Models.DataBase;
using VExtractor.Models.Results;

namespace VExtractor;

public class SqliteExporter
{
    private static KeyValuePair<int, string> GetEnumKeyValuePair(string input)
    {
        if (string.IsNullOrEmpty(input))
            return new KeyValuePair<int, string>(-1, string.Empty);
        var lastPart = input.Split('~').Last();
        if (int.TryParse(lastPart, out var key))
        {
            return new KeyValuePair<int, string>(key, input);
        }
        return new KeyValuePair<int, string>(-1, input);
    }

    private static string? CircuitOfGroupAddress(string? groupAddress)
    {
        if (string.IsNullOrEmpty(groupAddress)) return null;
        var parts = groupAddress.Split('~');
        foreach (var p in parts.Skip(1))
        {
            if (p is "HC1" or "HK1" or "Heizkreis1" or "SchaltzeitenHK1" || p.EndsWith("_HK1")) return "HC1";
            if (p is "HC2" or "HK2" or "Heizkreis2" or "SchaltzeitenHK2" || p.EndsWith("_HK2")) return "HC2";
            if (p is "HC3" or "HK3" or "Heizkreis3" or "SchaltzeitenHK3" || p.EndsWith("_HK3")) return "HC3";
            if (p is "WW" or "Warmwasser" or "SchaltzeitenWW") return "WW";
            if (p is "Solar" or "Solaranlage") return "Solar";
        }
        return null;
    }

    private static string? TierOf(string? rootAddress) =>
        string.IsNullOrEmpty(rootAddress) || rootAddress.StartsWith("ecnsysEventTypeGroupHC")
            ? null
            : rootAddress.Split('~').Last();

    private static readonly List<string> TierRank = new()
    {
        "Overview", "PlantOverview", "Operation", "Trending", "Statistic",
        "DiagnosisDiagnosis1", "DiagnosisDiagnosis2", "Installation", "Coding2", "CodeAccessLevelTD",
    };

    private static int TierPriority(string tier)
    {
        var idx = TierRank.IndexOf(tier);
        return idx == -1 ? TierRank.Count : idx;
    }

    /// <summary>
    /// The address of every menu group's root, the ancestor that has no parent, or null for a
    /// group outside any menu tree. Over the whole table rather than one controller's groups: a
    /// chain of parents can pass through a group belonging to another controller.
    /// </summary>
    internal static Dictionary<int, string?> RootAddresses(VDataBase vDb)
    {
        var parents = vDb.EcnEventTypeGroups.AsNoTracking()
            .Select(g => new { g.Id, g.ParentId, g.Address })
            .ToDictionary(g => g.Id, g => (g.ParentId, g.Address));

        string? RootAddressOf(int groupId)
        {
            var seen = new HashSet<int>();
            var current = groupId;
            while (seen.Add(current) && parents.TryGetValue(current, out var g))
            {
                if (g.ParentId == -1) return g.Address;
                current = g.ParentId;
            }
            return null;  // a chain that loops or breaks off: not part of any menu tree
        }

        return parents.Keys.ToDictionary(id => id, RootAddressOf);
    }

    /// <summary>
    /// The tier of each event type: the menu branch it sits in -- "Overview", "Operation",
    /// "Coding2" and so on -- which is the last segment of its group's root address. Both
    /// exporters use this, so they agree.
    /// </summary>
    internal static Dictionary<int, string> TiersOf(
        Dictionary<int, List<int>> groupIdsByEventTypeId, Dictionary<int, string?> rootAddresses)
    {
        var tiers = new Dictionary<int, string>();
        foreach (var (etId, gIds) in groupIdsByEventTypeId)
        {
            foreach (var gid in gIds)
            {
                if (TierOf(rootAddresses.GetValueOrDefault(gid)) is not { } tier) continue;
                if (!tiers.TryGetValue(etId, out var existing))
                {
                    tiers[etId] = tier;
                    continue;
                }
                // The most prominent branch wins. Branches outside the ranked list all rank
                // equally, so the name decides between them -- an arbitrary choice made the same
                // way every build rather than left to iteration order.
                var better = TierPriority(tier) - TierPriority(existing);
                if (better < 0 || (better == 0 && string.CompareOrdinal(tier, existing) < 0))
                    tiers[etId] = tier;
            }
        }
        return tiers;
    }

    private static readonly Dictionary<string, System.Reflection.PropertyInfo> AdditionalDataProperties =
        typeof(AdditionalData).GetProperties().ToDictionary(p => p.Name, StringComparer.Ordinal);

    /// <summary>
    /// The extension values of one event type, decoded into a flat record. Each value is a
    /// serialized .NET object, which is how the source stores them, and deserializing is the
    /// single most expensive thing done per datapoint; the caller therefore keeps the result
    /// and asks once per event type, not once per controller that has it.
    /// </summary>
    private static AdditionalData GetEventTypeAdditionalData(List<EcnTableExtensionValue> extensionValues, Dictionary<int, string> resultClassNames)
    {
        var result = new AdditionalData();
        foreach (var ecnTableExtensionValue in extensionValues)
        {
            _ = resultClassNames.TryGetValue(ecnTableExtensionValue.RefId, out var fieldName);
            fieldName = fieldName?.Split('.').Last();
            if (fieldName == null || !AdditionalDataProperties.TryGetValue(fieldName, out var property)) continue;

            var convertedValue = Convert.ChangeType(DataHelper.Deserialize(ecnTableExtensionValue.InternalValue),
                property.PropertyType);
            property.SetValue(result, convertedValue);
        }

        return result;
    }

    /// <summary>
    /// Bind a value on a command that is prepared once and executed many times. The parameter
    /// is created on first use and only its value changes afterwards, which is what keeps the
    /// prepared statement.
    /// </summary>
    private static void Bind(SqliteCommand command, string name, object? value)
    {
        var bound = value ?? DBNull.Value;
        if (command.Parameters.Contains(name)) command.Parameters[name].Value = bound;
        else command.Parameters.AddWithValue(name, bound);
    }

    /// <summary>One controller selected for export, with its full identification key.</summary>
    private sealed class SelectedDevice
    {
        public EcnDatapointType DataPointType = null!;
        public string Identification = "";
        public string? IdentExt;
        public string? IdentExtTill;
        public int? F0;
        public int? F0Till;
        public int? ControllerType;
        public int? ErrorType;
    }

    // EAV RefIds on ecnDatapointType (label.tableextension.ecnDatapointType.*).
    private const int RefIdentification = 6;
    private const int RefIdentExt = 7;
    private const int RefIdentExtTill = 8;
    private const int RefControllerType = 47;
    private const int RefErrorType = 60;
    private const int RefF0 = 318;
    private const int RefF0Till = 319;

    /// <summary>
    /// Name patterns for controllers this integration can never reach. It speaks Optolink only,
    /// whereas the source database also describes MBus meters, WILO pumps, DEKATEL units and
    /// the Vitocom/Vitotwin LAN-GSM gateways. Filtering them out takes the catalog from 354
    /// identifiable controller types down to the 253 that an IR head can actually talk to.
    /// </summary>
    private static readonly string[] NonOptolinkNameParts =
    {
        "MBus", "WILO", "DEKATEL", "Vitocom", "VCOM", "Vitodata", "Vitotwin", "BATI",
        // Fieldbus gateways (KNX/EIB/BACnet), not controllers reachable over an IR head.
        // Vitogate200EIB is also the only entry whose Identification is not four hex digits
        // ("208X"), which is a further sign it is not addressed the way a controller is.
        "Vitogate",
        // Not real hardware: a commissioning helper and a buffer-management pseudo device.
        "VirtualHydraulicCalibration", "puffermgm",
    };

    /// <summary>
    /// Choose which controllers to export. "default"/"all"/"optolink" selects every
    /// Optolink-reachable controller; otherwise the arguments are System IDs.
    ///
    /// Selection is per controller type, never per System ID: 253 Optolink controllers share
    /// only ~90 System IDs, so resolving an ID to a single controller is exactly the ambiguity
    /// that the IdentificationExtension and F0 stages exist to break.
    /// </summary>
    private static List<SelectedDevice> SelectDataPointTypes(
        VDataBase vDb, List<string> identifiers, Dictionary<int, string> resultClassNames)
    {
        var wantAll = identifiers == null || identifiers.Count == 0 ||
                      identifiers.Any(i => i.Equals("default", StringComparison.OrdinalIgnoreCase)
                                        || i.Equals("all", StringComparison.OrdinalIgnoreCase)
                                        || i.Equals("optolink", StringComparison.OrdinalIgnoreCase));
        var wanted = wantAll
            ? null
            : identifiers!.Select(i => i.Trim().ToUpperInvariant()).ToHashSet();

        var refIds = new[] { RefIdentification, RefIdentExt, RefIdentExtTill,
                             RefControllerType, RefErrorType, RefF0, RefF0Till };
        var ext = vDb.EcnTableExtensionValues.AsNoTracking()
            .Where(v => refIds.Contains(v.RefId))
            .ToList()
            .GroupBy(v => v.PkId)
            // One RefId can appear more than once per PkId when CompanyIds differ; take the
            // first rather than throwing on a duplicate key.
            .ToDictionary(g => g.Key,
                          g => g.GroupBy(v => v.RefId)
                                .ToDictionary(x => x.Key, x => x.First().InternalValue));

        string? Str(Dictionary<int, byte[]> d, int refId)
        {
            if (!d.TryGetValue(refId, out var raw)) return null;
            try { return DataHelper.Deserialize(raw)?.ToString(); } catch { return null; }
        }
        int? Int(Dictionary<int, byte[]> d, int refId)
        {
            var s = Str(d, refId);
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
        }

        var result = new List<SelectedDevice>();
        foreach (var dpt in vDb.EcnDatapointTypes.AsNoTracking().ToList())
        {
            if (!ext.TryGetValue(dpt.Id, out var fields)) continue;
            var identification = Str(fields, RefIdentification);
            if (string.IsNullOrWhiteSpace(identification)) continue;

            var name = dpt.Name ?? dpt.Address ?? "";
            if (NonOptolinkNameParts.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase)))
                continue;

            identification = identification.Trim().ToUpperInvariant();
            if (wanted != null && !wanted.Contains(identification)) continue;

            result.Add(new SelectedDevice
            {
                DataPointType = dpt,
                Identification = identification,
                IdentExt = Str(fields, RefIdentExt)?.Trim().ToUpperInvariant(),
                IdentExtTill = Str(fields, RefIdentExtTill)?.Trim().ToUpperInvariant(),
                F0 = Int(fields, RefF0),
                F0Till = Int(fields, RefF0Till),
                ControllerType = Int(fields, RefControllerType),
                ErrorType = Int(fields, RefErrorType),
            });
        }

        if (wanted != null)
        {
            foreach (var missing in wanted.Where(w => !result.Any(r => r.Identification == w)))
                Console.WriteLine($"Warning: System ID {missing} not found in database.");
        }
        return result.OrderBy(r => r.Identification).ThenBy(r => r.DataPointType.Id).ToList();
    }

    /// <summary>
    /// Fill each culture's missing translations from English, then German, then the key's own
    /// tail as a last resort.
    ///
    /// Viessmann's coverage is very uneven: measured over the 49,515 keys this catalog
    /// references, fourteen cultures sit at 95-96% but `hr` and `ro` reach only 14%, `sk` 15%
    /// and `no` 28%. Without a chain, those users would see raw identifiers like
    /// "WPR3_Aussentemperatur" for 85% of entities -- the exporter used to substitute the key
    /// tail, which looks like a translation but is not one.
    ///
    /// Filling here rather than at query time keeps the runtime SQL untouched, and it is nearly
    /// free: the shared `strings` pool stores the English text once no matter how many cultures
    /// point at it, so a filled gap costs one integer.
    /// </summary>
    private static void FillTranslationGaps(
        Dictionary<string, Dictionary<string, string>> byCulture, ICollection<string> keys)
    {
        byCulture.TryGetValue("en", out var en);
        byCulture.TryGetValue("de", out var de);

        foreach (var (culture, map) in byCulture)
        {
            if (culture is "en" or "de") continue;
            int fromEn = 0, fromDe = 0, fromKey = 0;
            foreach (var key in keys)
            {
                if (map.ContainsKey(key)) continue;
                if (en != null && en.TryGetValue(key, out var v)) { map[key] = v; fromEn++; }
                else if (de != null && de.TryGetValue(key, out v)) { map[key] = v; fromDe++; }
                else
                {
                    var tail = key.Split('~').Last();
                    if (!string.IsNullOrWhiteSpace(tail) && tail != key) { map[key] = tail; fromKey++; }
                }
            }
            if (fromEn + fromDe + fromKey > 0)
            {
                Console.WriteLine($"  {culture}: filled {fromEn} from en, {fromDe} from de, "
                                  + $"{fromKey} from key tail");
            }
        }

        // English and German fall back to each other, then to the key tail.
        foreach (var (culture, map) in byCulture)
        {
            if (culture is not ("en" or "de")) continue;
            var other = culture == "en" ? de : en;
            foreach (var key in keys)
            {
                if (map.ContainsKey(key)) continue;
                if (other != null && other.TryGetValue(key, out var v)) map[key] = v;
                else
                {
                    var tail = key.Split('~').Last();
                    if (!string.IsNullOrWhiteSpace(tail) && tail != key) map[key] = tail;
                }
            }
        }
    }

    /// <summary>
    /// Write the pivoted + interned translation store and the compatibility view over it.
    /// Culture columns are generated from the cultures actually exported, so adding a language
    /// needs no schema edit.
    /// </summary>
    private static void WriteTranslations(
        SqliteConnection connection, Dictionary<string, Dictionary<string, string>> byCulture)
    {
        var cultures = byCulture.Keys.OrderBy(c => c, StringComparer.Ordinal).ToList();
        if (cultures.Count == 0) return;

        // Column names come from a fixed culture vocabulary, but quote them anyway.
        string Col(string c) => "\"" + c.Replace("\"", "\"\"") + "\"";

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText =
                "CREATE TABLE strings (id INTEGER PRIMARY KEY, s TEXT NOT NULL);\n" +
                "CREATE TABLE translations_p (text_key TEXT PRIMARY KEY, " +
                string.Join(", ", cultures.Select(c => Col(c) + " INTEGER")) +
                ") WITHOUT ROWID;\n" +
                "CREATE VIEW translations AS\n" +
                string.Join("\nUNION ALL\n", cultures.Select(c =>
                    $"SELECT text_key, '{c}' AS culture, " +
                    $"(SELECT s FROM strings WHERE id = {Col(c)}) AS value " +
                    $"FROM translations_p WHERE {Col(c)} IS NOT NULL")) + ";";
            cmd.ExecuteNonQuery();
        }

        var allKeys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var m in byCulture.Values) allKeys.UnionWith(m.Keys);

        var pool = new Dictionary<string, int>(StringComparer.Ordinal);
        int Intern(string s)
        {
            if (!pool.TryGetValue(s, out var id)) { id = pool.Count + 1; pool[s] = id; }
            return id;
        }

        Console.WriteLine($"Writing {allKeys.Count} translation keys x {cultures.Count} culture(s)...");
        using (var tx = connection.BeginTransaction())
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT OR REPLACE INTO translations_p (text_key, " +
                              string.Join(", ", cultures.Select(Col)) + ") VALUES (@k, " +
                              string.Join(", ", cultures.Select((_, i) => "@v" + i)) + ")";
            var pk = cmd.Parameters.Add("@k", SqliteType.Text);
            var pv = cultures.Select((_, i) => cmd.Parameters.Add("@v" + i, SqliteType.Integer)).ToList();

            foreach (var key in allKeys)
            {
                pk.Value = key;
                for (var i = 0; i < cultures.Count; i++)
                {
                    pv[i].Value = byCulture[cultures[i]].TryGetValue(key, out var val)
                        ? Intern(val) : (object)DBNull.Value;
                }
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }

        using (var tx = connection.BeginTransaction())
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO strings (id, s) VALUES (@i, @s)";
            var pi = cmd.Parameters.Add("@i", SqliteType.Integer);
            var ps = cmd.Parameters.Add("@s", SqliteType.Text);
            foreach (var kv in pool) { pi.Value = kv.Value; ps.Value = kv.Key; cmd.ExecuteNonQuery(); }
            tx.Commit();
        }
        Console.WriteLine($"Interned {pool.Count} distinct translation strings.");
    }

    /// <summary>
    /// A whole source table, read on a thread of its own through a context of its own: a
    /// context cannot be shared between threads, and the source database takes concurrent
    /// readers. The export reads nine tables; side by side they take as long as the largest.
    /// </summary>
    private static Task<List<T>> ReadTable<T>(Func<VDataBase, DbSet<T>> table) where T : class =>
        Task.Run(() =>
        {
            using var db = new VDataBase();
            return table(db).AsNoTracking().ToList();
        });

    /// <summary>
    /// One language's texts: every label in the file, in file order, and the language's own
    /// value per label, the first one where a label repeats.
    /// </summary>
    private sealed record Texts(List<string> Labels, Dictionary<string, string> ByLabel);

    // A language file is 30 MB of XML and takes about half a second to parse, and one run needs
    // the same file for the controller list, the error codes and the translations. Lazy, so that
    // callers on different threads asking for the same file wait for one parse.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<Texts>> TextsByFile =
        new(StringComparer.Ordinal);

    /// <summary>The texts for a culture, parsed once per run. Throws when there is no file for it.</summary>
    private static Texts TextsFor(string culture)
    {
        var file = DataHelper.GetTranslationFilePath("Textresource_" + culture + ".xml");
        return TextsByFile.GetOrAdd(file, path => new Lazy<Texts>(() =>
        {
            var rows = new XmlDeserializer<DocumentElement>().ReadData(path)?.TextResources ?? new List<TextResource>();
            using var db = new VDataBase();
            var cultureId = db.EcnCultures.AsNoTracking().FirstOrDefault(a => a.Name.Equals(culture));
            var byLabel = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in rows.Where(a => cultureId == null || a.CultureId == cultureId.Id).GroupBy(a => a.Label))
                byLabel.TryAdd(group.Key, group.First().Value);
            return new Texts(rows.Select(t => t.Label).Distinct().ToList(), byLabel);
        })).Value;
    }

    public static void Export(VDataBase vDb, string dbPath, List<string> identifiers, List<string> cultures)
    {
        Console.WriteLine($"Starting SQLite Export to: {dbPath}");
        // The language files parse from here on, alongside everything up to the translations,
        // which pick the results up.
        foreach (var culture in cultures)
            _ = Task.Run(() => TextsFor(culture));
        var resultClassNames = vDb.EcnTableExtensions.AsNoTracking().ToDictionary(a => a.Id, a => a.Label);

        var rootAddresses = RootAddresses(vDb);
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;

                CREATE TABLE devices (
                    id INTEGER PRIMARY KEY,
                    system_id TEXT NOT NULL,
                    model TEXT NOT NULL,
                    name_key TEXT NOT NULL,
                    -- A controller is matched in three stages, and System ID alone is NOT
                    -- enough: across the Optolink catalog 253 controllers share only ~90
                    -- System IDs, and one of them covers fifteen variants whose datapoint
                    -- counts range from 561 to 1062.
                    --   1. identification      = <DeviceGroup:X2><Device:X2>   (the System ID)
                    --   2. ident_ext           = <HardwareIndex:X2><SoftwareIndex:X2>, matched
                    --      exactly or as the range ident_ext..ident_ext_till
                    --   3. f0..f0_till         only when Device is 0xC0..0xCB AND
                    --      SoftwareIndex >= 200; separates variants sharing one ident_ext
                    identification TEXT,
                    ident_ext TEXT,
                    ident_ext_till TEXT,
                    f0 INTEGER,
                    f0_till INTEGER,
                    controller_type INTEGER,
                    error_type INTEGER
                );
                CREATE INDEX idx_devices_system_id ON devices(system_id);

                CREATE TABLE circuits (
                    device_id INTEGER NOT NULL REFERENCES devices(id),
                    circuit TEXT NOT NULL,
                    name_key TEXT NOT NULL,
                    PRIMARY KEY (device_id, circuit)
                ) WITHOUT ROWID;

                CREATE TABLE groups (
                    id INTEGER PRIMARY KEY,
                    device_id INTEGER NOT NULL REFERENCES devices(id),
                    parent_id INTEGER,
                    address TEXT NOT NULL,
                    name_key TEXT NOT NULL,
                    order_index INTEGER NOT NULL
                );

                -- Datapoint DEFINITIONS are device-independent: an EventTypeId always carries
                -- the same address/geometry/conversion whichever controller references it
                -- (verified -- 2940 distinct ids yield 2940 distinct definitions). Only tier
                -- and circuit vary per device, so they live on device_datapoints below.
                -- Storing definitions once instead of once per device matters: the full
                -- Optolink catalog is 258 controllers and 90,770 device/datapoint pairs but
                -- only 8,292 distinct definitions, an 11x duplication if denormalised.
                CREATE TABLE datapoint_defs (
                    id INTEGER PRIMARY KEY,
                    address TEXT NOT NULL,
                    name TEXT NOT NULL,
                    name_key TEXT NOT NULL,
                    description_key TEXT,
                    byte_length INTEGER NOT NULL,
                    block_length INTEGER NOT NULL,
                    byte_position INTEGER NOT NULL,
                    bit_start INTEGER NOT NULL,
                    bit_length INTEGER NOT NULL,
                    conversion TEXT NOT NULL,
                    unit TEXT,
                    entity_kind INTEGER NOT NULL,
                    parameter_type TEXT,
                    min_value REAL,
                    max_value REAL,
                    stepping REAL,
                    fc_read TEXT,
                    fc_write TEXT,
                    prefix_read TEXT,
                    block_factor INTEGER NOT NULL DEFAULT 0,
                    -- sdk_data_type is the value's declared type. Exported because it is real
                    -- catalog data, but deliberately NOT used to gate conversions: the catalog
                    -- contradicts itself on a handful of datapoints, where gating would push a
                    -- value outside its own declared min/max. See the integration's decoder.
                    sdk_data_type TEXT,
                    -- Only MultOffset uses these (IntData * factor + offset), but without them
                    -- that conversion cannot be evaluated at all.
                    conversion_factor REAL,
                    conversion_offset REAL,
                    -- Priority is the catalog's own urgency ranking (lower = more urgent),
                    -- used to decide poll cadence instead of reading everything at one flat
                    -- interval.
                    priority INTEGER,
                    -- Structure of a block datapoint whose bytes are not a plain value:
                    --   1, 9  weekly programme, 7 days x 4 windows x (start, end)
                    --   2     weekly programme, 7 days x 24 bytes of 2-bit quarter-hour levels
                    --   5..8  weekly programme, 7 days x 8 windows x (start, end, level);
                    --         5 heating circuit, 6 hot water, 7 circulation, 8 buffer
                    --   10    ventilation programme, same layout as 5 with levels 2..4
                    --   3, 4  error history (boiler / heat pump layout)
                    -- 0 for everything else. Read from the MappingType extension of the
                    -- event type.
                    mapping_type INTEGER NOT NULL DEFAULT 0,
                    -- Where a weekly programme's level names live, as a text key with the level
                    -- number left off: the stem, a dot and the level is the key. Two of them,
                    -- because a handful of programmes (ventilation, electric heater) carry their
                    -- own wording filed under the datapoint, and everything else uses the wording
                    -- of its programme type. The consumer tries the datapoint's stem first.
                    -- NULL for datapoints that are not weekly programmes. These exist so that the
                    -- reader never has to know how the source names its texts.
                    level_text_stem TEXT,
                    level_text_stem_dp TEXT
                );

                -- Which controller exposes which datapoint, plus the two attributes that are
                -- genuinely per-device because they come from device-scoped group membership.
                CREATE TABLE device_datapoints (
                    device_id INTEGER NOT NULL REFERENCES devices(id),
                    datapoint_id INTEGER NOT NULL REFERENCES datapoint_defs(id),
                    tier TEXT,
                    circuit TEXT,
                    PRIMARY KEY (device_id, datapoint_id)
                ) WITHOUT ROWID;

                -- What this catalog is, for the reader to check before trusting it. Key and
                -- value rather than columns, so that adding a fact later is one INSERT and not
                -- a schema change of its own.
                CREATE TABLE catalog_meta (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                ) WITHOUT ROWID;

                -- Compatibility view: presents the old flat shape, so every existing
                -- `SELECT ... FROM datapoints WHERE device_id = ?` keeps working unchanged.
                CREATE VIEW datapoints AS
                SELECT dd.device_id, d.id, d.address, d.name, d.name_key, d.description_key,
                       d.byte_length, d.block_length, d.byte_position, d.bit_start, d.bit_length,
                       d.conversion, d.unit, dd.tier, dd.circuit, d.entity_kind, d.parameter_type,
                       d.min_value, d.max_value, d.stepping, d.fc_read, d.fc_write, d.prefix_read,
                       d.block_factor, d.sdk_data_type, d.conversion_factor, d.conversion_offset,
                       d.priority, d.mapping_type, d.level_text_stem, d.level_text_stem_dp
                FROM device_datapoints dd
                JOIN datapoint_defs d ON d.id = dd.datapoint_id;

                -- Per-device fault codes. The text key is viessmann.errorcode.<Model>.<CODE>:
                -- error texts are DEVICE-SPECIFIC and differ substantially from the generic
                -- viessmann.errorcode.<CODE> set (on a heat pump FF means Neustart der Regelung,
                -- not the boiler meaning Interner Fehler oder Reset-Taster blockiert).
                CREATE TABLE error_codes (
                    device_id INTEGER NOT NULL REFERENCES devices(id),
                    code TEXT NOT NULL,
                    text_key TEXT NOT NULL,
                    PRIMARY KEY (device_id, code)
                ) WITHOUT ROWID;

                CREATE TABLE enums (
                    event_type_id INTEGER NOT NULL,
                    val_key INTEGER NOT NULL,
                    text_key TEXT NOT NULL,
                    PRIMARY KEY (event_type_id, val_key)
                ) WITHOUT ROWID;

                -- WITHOUT ROWID throughout the link tables below. Their primary key already
                -- covers the identifying columns, so the default layout keeps a rowid heap AND
                -- a full autoindex duplicating those columns. Measured over the 253-controller
                -- catalog this duplication costs 3.9 MB: datapoint_groups 3412->1288 KB,
                -- device_datapoints 3456->2016 KB, enums 876->712 KB, error_codes 820->612 KB.
                -- Lookup speed is unchanged (~18 us).
                CREATE TABLE datapoint_groups (
                    event_type_id INTEGER NOT NULL,
                    group_id INTEGER NOT NULL,
                    PRIMARY KEY (event_type_id, group_id)
                ) WITHOUT ROWID;

                CREATE TABLE display_conditions (
                    id INTEGER NOT NULL,
                    device_id INTEGER NOT NULL REFERENCES devices(id),
                    target_event_type_id INTEGER,
                    target_group_id INTEGER,
                    condition_event_type_id INTEGER NOT NULL,
                    op TEXT NOT NULL,
                    compare_value INTEGER NOT NULL,
                    rule_type INTEGER NOT NULL
                );
                CREATE INDEX idx_conditions_device ON display_conditions(device_id);

                -- `translations` is created later by WriteTranslations() as a view over a
                -- pivoted, value-interned store; see the comment there for the measurements.
            ";
            cmd.ExecuteNonQuery();
        }

        var referencedTextKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Where the time goes, per section of the controller loop. Printed when
        // VEXTRACTOR_PROFILE is set; costs nothing worth mentioning otherwise.
        var profile = new Dictionary<string, TimeSpan>();
        var lapClock = System.Diagnostics.Stopwatch.StartNew();
        void Lap(string section)
        {
            profile[section] = profile.GetValueOrDefault(section) + lapClock.Elapsed;
            lapClock.Restart();
        }

        // Track devices processed
        var processedDeviceIds = new HashSet<int>();

        using (var tx = connection.BeginTransaction())
        {
            var selected = SelectDataPointTypes(vDb, identifiers, resultClassNames);
            Console.WriteLine($"Selected {selected.Count} controller(s) for export.");

            // Everything the loop needs, read once and kept in memory. The per-controller
            // queries this replaces each scanned a table of up to a quarter of a million rows,
            // once per controller. The whole set is about 10 MB of payload, and a lookup is
            // what the loop actually wants. Lists keep the order the tables store, so that
            // every "first one wins" below decides the way it always has.
            var groupsRead = ReadTable(db => db.EcnEventTypeGroups);
            var eventTypeLinksRead = ReadTable(db => db.EcnDataPointTypeEventTypeLinks);
            var eventTypesRead = ReadTable(db => db.EcnEventTypes);
            var valueLinksRead = ReadTable(db => db.EcnEventTypeEventValueTypeLinks);
            var valueTypesRead = ReadTable(db => db.EcnEventValueTypes);
            var extensionValuesRead = ReadTable(db => db.EcnTableExtensionValues);
            var groupLinksRead = ReadTable(db => db.EcnEventTypeEventTypeGroupLinks);
            var conditionGroupsRead = ReadTable(db => db.EcnDisplayConditionGroups);
            var conditionsRead = ReadTable(db => db.EcnDisplayConditions);
            Task.WhenAll(groupsRead, eventTypeLinksRead, eventTypesRead, valueLinksRead, valueTypesRead,
                extensionValuesRead, groupLinksRead, conditionGroupsRead, conditionsRead).GetAwaiter().GetResult();

            var groupsByDataPointType = groupsRead.Result
                .GroupBy(g => g.DataPointTypeId).ToDictionary(g => g.Key, g => g.ToList());
            var eventTypeIdsByDataPointType = eventTypeLinksRead.Result
                .GroupBy(l => l.DataPointTypeId).ToDictionary(g => g.Key, g => g.Select(l => l.EventTypeId).ToList());
            var allEventTypes = eventTypesRead.Result;
            var valueLinksByEventType = valueLinksRead.Result
                .GroupBy(l => l.EventTypeId).ToDictionary(g => g.Key, g => g.ToList());
            var valueTypesById = valueTypesRead.Result.ToDictionary(v => v.Id);
            var extensionValuesByPk = extensionValuesRead.Result
                .GroupBy(v => v.PkValue, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
            var allGroupLinks = groupLinksRead.Result;
            var allConditionGroups = conditionGroupsRead.Result;
            var allDisplayConditions = conditionsRead.Result;
            Lap("loading source tables");

            // A definition is device-independent, so each is decoded and written once however
            // many controllers carry it. Later controllers only add their link to it.
            var decodedByPk = new Dictionary<string, AdditionalData>(StringComparer.Ordinal);
            var writtenDefinitions = new HashSet<int>();
            var eventTypesById = allEventTypes.ToDictionary(e => e.Id);

            // The link and condition tables by group, built once. Scanning them in full for every
            // controller cost seconds on a full build. Rows stay in table order within a group;
            // group links carry their row number so a controller's links can be put back in
            // table order across its groups.
            var groupLinksByGroup = allGroupLinks.Select((link, row) => (link, row))
                .ToLookup(l => l.link.EventTypeGroupId);
            var conditionsByGroup = allDisplayConditions.ToLookup(c => c.ConditionGroupId);

            // An event type with its extension values and value types, or null when it has no
            // extension values (and therefore no address, and no place in the catalog).
            EcnEventTypeDto? DtoFor(EcnEventType eventType)
            {
                if (!extensionValuesByPk.TryGetValue($"{eventType.Id};{eventType.CompanyId}", out var extensionValues))
                    return null;
                var links = valueLinksByEventType.GetValueOrDefault(eventType.Id);
                return new EcnEventTypeDto
                {
                    EventType = eventType,
                    ExtensionValues = extensionValues,
                    EventValuesTypes = links == null
                        ? new List<EcnEventValueType>()
                        : links.Select(l => valueTypesById.GetValueOrDefault(l.EventValueId))
                               .OfType<EcnEventValueType>().ToList(),
                };
            }

            AdditionalData Decode(EcnEventTypeDto dto)
            {
                var pk = $"{dto.EventType.Id};{dto.EventType.CompanyId}";
                if (!decodedByPk.TryGetValue(pk, out var data))
                    decodedByPk[pk] = data = GetEventTypeAdditionalData(dto.ExtensionValues!, resultClassNames);
                return data;
            }

            // Prepared once; see Bind().
            using var insertDefinition = connection.CreateCommand();
            insertDefinition.Transaction = tx;
            insertDefinition.CommandText = @"INSERT OR IGNORE INTO datapoint_defs (
                id, address, name, name_key, description_key,
                byte_length, block_length, byte_position, bit_start, bit_length,
                conversion, unit, entity_kind, parameter_type,
                min_value, max_value, stepping,
                fc_read, fc_write, prefix_read, block_factor,
                sdk_data_type, conversion_factor, conversion_offset, priority,
                mapping_type, level_text_stem, level_text_stem_dp
            ) VALUES (
                @id, @addr, @name, @nkey, @dkey,
                @blen, @blklen, @bpos, @bstart, @bitlen,
                @conv, @unit, @kind, @ptype,
                @min, @max, @step,
                @fcread, @fcwrite, @prefix, @blkfactor,
                @sdktype, @cfactor, @coffset, @prio,
                @maptype, @lvlstem, @lvlstemdp
            )";
            using var linkDatapoint = connection.CreateCommand();
            linkDatapoint.Transaction = tx;
            linkDatapoint.CommandText =
                "INSERT OR REPLACE INTO device_datapoints (device_id, datapoint_id, tier, circuit) VALUES (@dev_id, @id, @tier, @circ)";
            using var insertEnum = connection.CreateCommand();
            insertEnum.Transaction = tx;
            insertEnum.CommandText = "INSERT OR REPLACE INTO enums (event_type_id, val_key, text_key) VALUES (@etid, @vkey, @tkey)";
            using var insertGroupLink = connection.CreateCommand();
            insertGroupLink.Transaction = tx;
            insertGroupLink.CommandText = "INSERT OR IGNORE INTO datapoint_groups (event_type_id, group_id) VALUES (@etid, @gid)";
            using var insertGroup = connection.CreateCommand();
            insertGroup.Transaction = tx;
            insertGroup.CommandText =
                "INSERT OR REPLACE INTO groups (id, device_id, parent_id, address, name_key, order_index) VALUES (@id, @dev_id, @pid, @addr, @nkey, @ord)";
            using var insertCondition = connection.CreateCommand();
            insertCondition.Transaction = tx;
            insertCondition.CommandText =
                "INSERT INTO display_conditions (id, device_id, target_event_type_id, target_group_id, condition_event_type_id, op, compare_value, rule_type) "
                + "VALUES (@id, @dev_id, @tet, @tg, @cet, @op, @cmp, @rt)";

            // One definition row plus its enums. Called once per event type, from whichever
            // controller meets it first, and once more below for inputs that only rules name.
            void WriteDefinition(EcnEventTypeDto evTypeDto, AdditionalData additionalData)
            {
                var etId = evTypeDto.EventType.Id;
                var nameKey = evTypeDto.EventType.Name;
                var descKey = evTypeDto.EventType.Description;
                var unitKey = evTypeDto.EventValuesTypes?.FirstOrDefault()?.Unit;
                referencedTextKeys.Add(nameKey);
                if (!string.IsNullOrEmpty(descKey)) referencedTextKeys.Add(descKey);
                if (!string.IsNullOrEmpty(unitKey)) referencedTextKeys.Add(unitKey);
                var lowerLimit = evTypeDto.EventValuesTypes?.Min(a => a.LowerBorder);
                var upperLimit = evTypeDto.EventValuesTypes?.Max(a => a.UpperBorder);
                var stepping = evTypeDto.EventValuesTypes?.FirstOrDefault()?.Stepping;

                var cmd = insertDefinition;
                Bind(cmd, "@id", etId);
                Bind(cmd, "@addr", additionalData.Address);
                Bind(cmd, "@name", evTypeDto.EventType.Name.Split('~').Last());
                Bind(cmd, "@nkey", nameKey);
                Bind(cmd, "@dkey", string.IsNullOrEmpty(descKey) ? null : descKey);
                Bind(cmd, "@blen", additionalData.ByteLength);
                Bind(cmd, "@blklen", additionalData.BlockLength);
                Bind(cmd, "@bpos", additionalData.BytePosition);
                Bind(cmd, "@bstart", additionalData.BitPosition);
                Bind(cmd, "@bitlen", additionalData.BitLength);
                Bind(cmd, "@conv", evTypeDto.EventType.Conversion);
                Bind(cmd, "@unit", string.IsNullOrEmpty(unitKey) ? null : unitKey);
                Bind(cmd, "@kind", evTypeDto.EventType.Type);
                Bind(cmd, "@ptype", string.IsNullOrEmpty(additionalData.Parameter) ? null : additionalData.Parameter);
                Bind(cmd, "@min", lowerLimit);
                Bind(cmd, "@max", upperLimit);
                Bind(cmd, "@step", stepping);
                // FCRead decides the wire operation: "Virtual_READ" is an ordinary memory
                // read, "Remote_Procedure_Call" needs P300 function code 7 plus a PrefixRead
                // index parameter. BlockFactor is the array element count, so
                // ByteLength / BlockFactor is the size of one element.
                Bind(cmd, "@fcread", string.IsNullOrEmpty(additionalData.FCRead) ? null : additionalData.FCRead);
                Bind(cmd, "@fcwrite", string.IsNullOrEmpty(additionalData.FCWrite) ? null : additionalData.FCWrite);
                Bind(cmd, "@prefix", string.IsNullOrEmpty(additionalData.PrefixRead) ? null : additionalData.PrefixRead);
                Bind(cmd, "@blkfactor", int.TryParse(additionalData.BlockFactor, out var bf) ? bf : 0);
                // SDKDataType (Int/Double/Byte/ByteArray/String/DateTime) selects which
                // conversion applies; the numeric factors are only consumed by
                // MultOffset. Parsed invariantly -- they are stored as strings and a
                // German locale would otherwise read "0,1" as 1.
                Bind(cmd, "@sdktype", string.IsNullOrEmpty(additionalData.SDKDataType) ? null : additionalData.SDKDataType);
                Bind(cmd, "@cfactor",
                    double.TryParse(additionalData.ConversionFactor, NumberStyles.Float, CultureInfo.InvariantCulture, out var cf)
                        ? cf : null);
                Bind(cmd, "@coffset",
                    double.TryParse(additionalData.ConversionOffset, NumberStyles.Float, CultureInfo.InvariantCulture, out var co)
                        ? co : null);
                Bind(cmd, "@prio", evTypeDto.EventType.Priority);
                var mappingType = int.TryParse(additionalData.MappingField, out var mt) ? mt : 0;
                Bind(cmd, "@maptype", mappingType);
                var (levelStem, levelStemDp) = ScheduleLevelStems(
                    mappingType, evTypeDto.EventType.Name, additionalData.Address);
                Bind(cmd, "@lvlstem", levelStem);
                Bind(cmd, "@lvlstemdp", levelStemDp);
                cmd.ExecuteNonQuery();

                // Enums belong to the definition, so they are written with it.
                if (evTypeDto.EventType.EnumType && evTypeDto.EventValuesTypes != null)
                {
                    foreach (var evt in evTypeDto.EventValuesTypes)
                    {
                        if (string.IsNullOrEmpty(evt.EnumReplaceValue)) continue;
                        var kv = GetEnumKeyValuePair(evt.EnumReplaceValue);
                        if (kv.Key == -1) continue;
                        referencedTextKeys.Add(kv.Value);
                        Bind(insertEnum, "@etid", etId);
                        Bind(insertEnum, "@vkey", kv.Key);
                        Bind(insertEnum, "@tkey", kv.Value);
                        insertEnum.ExecuteNonQuery();
                    }
                }
            }

            foreach (var sel in selected)
            {
                var foundDataPoint = sel.DataPointType;
                var cleanId = sel.Identification;

                var deviceId = foundDataPoint.Id;
                var deviceNameKey = foundDataPoint.Description ?? foundDataPoint.Name ?? foundDataPoint.Address;
                referencedTextKeys.Add(deviceNameKey);

                // Insert into devices, carrying the full identification key. Storing only the
                // System ID and taking the first controller that matches it silently picks one
                // of up to fifteen variants that share an ID.
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"INSERT OR REPLACE INTO devices
                        (id, system_id, model, name_key, identification, ident_ext, ident_ext_till,
                         f0, f0_till, controller_type, error_type)
                        VALUES (@id, @sys_id, @model, @name_key, @ident, @ext, @exttill,
                                @f0, @f0till, @ctype, @etype)";
                    cmd.Parameters.AddWithValue("@id", deviceId);
                    cmd.Parameters.AddWithValue("@sys_id", cleanId);
                    cmd.Parameters.AddWithValue("@model", foundDataPoint.Address);
                    cmd.Parameters.AddWithValue("@name_key", deviceNameKey);
                    cmd.Parameters.AddWithValue("@ident", cleanId);
                    cmd.Parameters.AddWithValue("@ext", (object?)sel.IdentExt ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@exttill", (object?)sel.IdentExtTill ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@f0", (object?)sel.F0 ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@f0till", (object?)sel.F0Till ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ctype", (object?)sel.ControllerType ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@etype", (object?)sel.ErrorType ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }

                if (processedDeviceIds.Contains(deviceId))
                {
                    continue;
                }
                processedDeviceIds.Add(deviceId);

                Console.WriteLine($"Extracting device: {foundDataPoint.Address} (Id: {deviceId}, SysId: {cleanId}, Ext: {sel.IdentExt ?? "-"})...");
                lapClock.Restart();

                // Groups
                var fullGroups = (groupsByDataPointType.GetValueOrDefault(foundDataPoint.Id) ?? new List<EcnEventTypeGroup>())
                    .OrderBy(g => g.ParentId)
                    .ThenBy(g => g.OrderIndex)
                    .ToList();

                var familyGroupIds = fullGroups.Select(g => g.Id).ToHashSet();
                var familyGroupAddress = fullGroups.ToDictionary(g => g.Id, g => g.Address);

                string GroupNameKey(EcnEventTypeGroup g)
                {
                    if (!string.IsNullOrEmpty(g.Description)) return g.Description;
                    if (!string.IsNullOrEmpty(g.Name)) return g.Name;
                    return "ecnsysEventTypeGroup~" + g.Address;
                }

                foreach (var g in fullGroups)
                {
                    var gKey = GroupNameKey(g);
                    referencedTextKeys.Add(gKey);

                    Bind(insertGroup, "@id", g.Id);
                    Bind(insertGroup, "@dev_id", deviceId);
                    Bind(insertGroup, "@pid", g.ParentId == -1 ? (int?)null : g.ParentId);
                    Bind(insertGroup, "@addr", g.Address);
                    Bind(insertGroup, "@nkey", gKey);
                    Bind(insertGroup, "@ord", g.OrderIndex);
                    insertGroup.ExecuteNonQuery();
                }

                Lap("groups");
                // Circuits
                var circuitNameKeys = new Dictionary<string, string>();
                foreach (var g in fullGroups.Where(g => g.Address.Contains("~Overview~")))
                {
                    var c = CircuitOfGroupAddress(g.Address);
                    if (c != null && !circuitNameKeys.ContainsKey(c))
                        circuitNameKeys[c] = GroupNameKey(g);
                }
                foreach (var g in fullGroups)
                {
                    var c = CircuitOfGroupAddress(g.Address);
                    if (c != null && !circuitNameKeys.ContainsKey(c))
                        circuitNameKeys[c] = GroupNameKey(g);
                }

                foreach (var (cKey, nKey) in circuitNameKeys)
                {
                    referencedTextKeys.Add(nKey);
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "INSERT OR REPLACE INTO circuits (device_id, circuit, name_key) VALUES (@dev_id, @circ, @nkey)";
                    cmd.Parameters.AddWithValue("@dev_id", deviceId);
                    cmd.Parameters.AddWithValue("@circ", cKey);
                    cmd.Parameters.AddWithValue("@nkey", nKey);
                    cmd.ExecuteNonQuery();
                }

                Lap("circuits");
                // Event types
                var eventTypeIdSet = (eventTypeIdsByDataPointType.GetValueOrDefault(foundDataPoint.Id) ?? new List<int>()).ToHashSet();
                var eventTypeList = allEventTypes.Where(a => eventTypeIdSet.Contains(a.Id)).ToList();

                Lap("event type queries");
                // Group links
                var groupLinks = familyGroupIds
                    .SelectMany(gid => groupLinksByGroup[gid])
                    .Where(l => eventTypeIdSet.Contains(l.link.EventTypeId))
                    .OrderBy(l => l.row)
                    .Select(l => l.link)
                    .ToList();

                var groupIdsByEventTypeId = groupLinks.GroupBy(l => l.EventTypeId)
                    .ToDictionary(g => g.Key, g => g.Select(l => l.EventTypeGroupId).Distinct().ToList());

                foreach (var link in groupLinks)
                {
                    Bind(insertGroupLink, "@etid", link.EventTypeId);
                    Bind(insertGroupLink, "@gid", link.EventTypeGroupId);
                    insertGroupLink.ExecuteNonQuery();
                }

                var circuitByEventTypeId = new Dictionary<int, string>();
                foreach (var (etId, gIds) in groupIdsByEventTypeId)
                {
                    var matchedCircuits = gIds
                        .Select(gid => familyGroupAddress.TryGetValue(gid, out var addr) ? CircuitOfGroupAddress(addr) : null)
                        .Where(c => c != null).Distinct().ToList();
                    if (matchedCircuits.Count == 1)
                        circuitByEventTypeId[etId] = matchedCircuits[0]!;
                }

                var tierByEventTypeId = TiersOf(groupIdsByEventTypeId, rootAddresses);

                Lap("group links, tiers, circuits");
                // Display conditions
                var conditionGroups = allConditionGroups
                    .Where(g => (g.EventTypeIdDest != -1 && eventTypeIdSet.Contains(g.EventTypeIdDest))
                             || (g.EventTypeGroupIdDest != -1 && familyGroupIds.Contains(g.EventTypeGroupIdDest)))
                    .ToList();

                foreach (var group in conditionGroups)
                {
                    var targetEtId = group.EventTypeIdDest == -1 ? (int?)null : group.EventTypeIdDest;
                    var targetGId = group.EventTypeGroupIdDest == -1 ? (int?)null : group.EventTypeGroupIdDest;
                    var ruleType = group.Type; // 1 = AND, 2 = OR

                    foreach (var cond in conditionsByGroup[group.Id])
                    {
                        var op = cond.Condition switch { 0 => "eq", 1 => "ne", 2 => "gt", 4 => "lt", _ => "eq" };
                        var compareValue = valueTypesById.TryGetValue(cond.EventTypeValueCondition, out var valType)
                            ? GetEnumKeyValuePair(valType.EnumReplaceValue ?? string.Empty).Key
                            : (int)(cond.ConditionValue ?? 0);

                        Bind(insertCondition, "@id", cond.Id);
                        Bind(insertCondition, "@dev_id", deviceId);
                        Bind(insertCondition, "@tet", targetEtId);
                        Bind(insertCondition, "@tg", targetGId);
                        Bind(insertCondition, "@cet", cond.EventTypeIdCondition);
                        Bind(insertCondition, "@op", op);
                        Bind(insertCondition, "@cmp", compareValue);
                        Bind(insertCondition, "@rt", ruleType);
                        insertCondition.ExecuteNonQuery();
                    }
                }

                Lap("display conditions");
                // Prepare datapoints
                // Event types that carry extension values, in the order the source lists them,
                // each with its value types in link order.
                var finalList = eventTypeList.Select(DtoFor).OfType<EcnEventTypeDto>().ToList();

                Lap("datapoint list");
                foreach (var evTypeDto in finalList)
                {
                    var additionalData = Decode(evTypeDto);
                    Lap("datapoint decode");
                    if (string.IsNullOrWhiteSpace(additionalData.Address)) continue;

                    var etId = evTypeDto.EventType.Id;
                    tierByEventTypeId.TryGetValue(etId, out var tier);
                    circuitByEventTypeId.TryGetValue(etId, out var circuit);

                    // The definition is device-independent, so the first controller that
                    // carries a datapoint writes it and the rest only add their link. The
                    // link references the definition, hence the order.
                    if (writtenDefinitions.Add(etId))
                        WriteDefinition(evTypeDto, additionalData);

                    Bind(linkDatapoint, "@dev_id", deviceId);
                    Bind(linkDatapoint, "@id", etId);
                    Bind(linkDatapoint, "@tier", string.IsNullOrEmpty(tier) ? null : tier);
                    Bind(linkDatapoint, "@circ", string.IsNullOrEmpty(circuit) ? null : circuit);
                    linkDatapoint.ExecuteNonQuery();
                    Lap("datapoint sql");
                }
            }

            // Inputs of display conditions that belong to no exported controller. A rule on one
            // controller often tests a datapoint that only a sibling family exposes; the reader
            // bridges that by the label of the foreign definition, which therefore has to be in
            // the catalog even though no exported controller links to it. A full build has them
            // all anyway; a build for one controller would otherwise silently lose the rules.
            var foreignInputs = new List<int>();
            using (var query = connection.CreateCommand())
            {
                query.Transaction = tx;
                query.CommandText = "SELECT DISTINCT condition_event_type_id FROM display_conditions";
                using var reader = query.ExecuteReader();
                while (reader.Read())
                {
                    var id = reader.GetInt32(0);
                    if (!writtenDefinitions.Contains(id)) foreignInputs.Add(id);
                }
            }
            var added = 0;
            foreach (var id in foreignInputs.OrderBy(i => i))
            {
                if (!eventTypesById.TryGetValue(id, out var eventType)) continue;
                var dto = DtoFor(eventType);
                if (dto == null) continue;
                var data = Decode(dto);
                if (string.IsNullOrWhiteSpace(data.Address)) continue;
                writtenDefinitions.Add(id);
                WriteDefinition(dto, data);
                added++;
            }
            if (added > 0)
                Console.WriteLine($"Added {added} definitions that only display conditions refer to.");
            Lap("rule-only definitions");
            tx.Commit();
        }

        if (Environment.GetEnvironmentVariable("VEXTRACTOR_PROFILE") != null)
            foreach (var (section, spent) in profile.OrderByDescending(kv => kv.Value))
                Console.WriteLine($"  profile  {spent.TotalSeconds,6:F1}s  {section}");

        Console.WriteLine($"Discovered {referencedTextKeys.Count} unique text keys across exported devices.");

        // Fault codes per device. These are NOT in the .mdf -- they live only in the
        // Textresource_<culture>.xml files under the key viessmann.errorcode.<Model>.<CODE>.
        // Scan the first available culture file for each exported device's model prefix; the
        // resulting text keys are then translated for every requested culture below.
        {
            var modelsByDeviceId = new Dictionary<int, string>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT id, model FROM devices";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    modelsByDeviceId[reader.GetInt32(0)] = reader.GetString(1);
            }

            var scanCulture = cultures.FirstOrDefault(c =>
                File.Exists(DataHelper.GetTranslationFilePath("Textresource_" + c + ".xml")));

            if (scanCulture == null)
            {
                Console.WriteLine("Warning: no translation file available to scan for error codes.");
            }
            else
            {
                var allLabels = TextsFor(scanCulture).Labels;

                using var tx = connection.BeginTransaction();
                var totalCodes = 0;
                foreach (var (devId, model) in modelsByDeviceId)
                {
                    var prefix = $"viessmann.errorcode.{model}.";
                    foreach (var label in allLabels)
                    {
                        if (label == null || !label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                        var code = label.Substring(prefix.Length).ToUpperInvariant();
                        if (code.Length == 0 || code.Contains('.')) continue;

                        referencedTextKeys.Add(label);
                        using var cmd = connection.CreateCommand();
                        cmd.Transaction = tx;
                        cmd.CommandText = "INSERT OR REPLACE INTO error_codes (device_id, code, text_key) VALUES (@d, @c, @k)";
                        cmd.Parameters.AddWithValue("@d", devId);
                        cmd.Parameters.AddWithValue("@c", code);
                        cmd.Parameters.AddWithValue("@k", label);
                        cmd.ExecuteNonQuery();
                        totalCodes++;
                    }
                }
                tx.Commit();
                Console.WriteLine($"Exported {totalCodes} device-specific error codes (scanned culture '{scanCulture}').");
            }
        }

        // Translations export.
        //
        // Layout matters here: translations are the single largest thing in the catalog. Stored
        // naively (text_key, culture, value) they were 9.7 MB of a 24 MB database, because the
        // ~52-character text key is repeated once per culture and the values repeat heavily
        // (99,343 rows carry only 26,119 distinct strings). Two changes, both measured:
        //   * pivot  -- one row per key with a column per culture, so the key is stored once
        //   * intern -- values live in a shared `strings` pool and the row holds an integer
        // Together: 9.7 MB -> 4.5 MB, a 54% cut, with lookup performance unchanged because the
        // `translations` view below preserves the original (text_key, culture, value) shape and
        // the text_key predicate still resolves through the primary key.
        //
        // Interning the *keys* as well was tried and is counterproductive: the unique index
        // needed to deduplicate them costs more than the repetition it removes (10.3 MB).
        var translationsByCulture = new Dictionary<string, Dictionary<string, string>>();
        foreach (var culture in cultures)
        {
            var translationFile = DataHelper.GetTranslationFilePath("Textresource_" + culture + ".xml");
            if (!File.Exists(translationFile))
            {
                Console.WriteLine($"Warning: Translation file for culture '{culture}' not found ({translationFile}). Skipping.");
                continue;
            }

            Console.WriteLine($"Loading translations for culture: {culture}...");
            var textResources = TextsFor(culture).ByLabel;

            // Only a genuine hit counts. The "@@" prefix is inconsistently applied in the
            // vendor data, so try the key both ways -- skipping that makes coverage look like
            // 33% when it is really 96%.
            string? TryTranslate(string key)
            {
                if (string.IsNullOrEmpty(key)) return null;
                if (textResources.TryGetValue(key, out var val)) return val;
                if (key.StartsWith("@@") && textResources.TryGetValue(key.Substring(2), out val)) return val;
                if (!key.StartsWith("@@") && textResources.TryGetValue("@@" + key, out val)) return val;
                return null;
            }

            // The schedule editor's level names (Standby, Reduziert, Normal, Festwert and the
            // hot-water / circulation / buffer variants) are not referenced by any datapoint or
            // enum; they hang off the schedule's MappingType instead, under
            // viessmann.CircuitTimes.<type>[.<name>~<address>].Value.<level>. Take them all.
            foreach (var label in textResources.Keys)
            {
                if (label.StartsWith("viessmann.CircuitTimes.", StringComparison.OrdinalIgnoreCase))
                    referencedTextKeys.Add(label);
            }

            var forCulture = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var key in referencedTextKeys)
            {
                var translated = TryTranslate(key);
                if (string.IsNullOrWhiteSpace(translated) || translated == key) continue;
                forCulture[key] = translated!;
            }
            Console.WriteLine($"  {culture}: {forCulture.Count}/{referencedTextKeys.Count} translated "
                              + $"({(double)forCulture.Count / Math.Max(1, referencedTextKeys.Count):P0})");
            translationsByCulture[culture.ToLowerInvariant()] = forCulture;
        }

        FillTranslationGaps(translationsByCulture, referencedTextKeys);
        WriteTranslations(connection, translationsByCulture);

        // Written last, so that a catalog only claims a version once everything in it is there.
        // An interrupted build leaves a file with no version, which the reader rejects.
        //
        // Nothing that varies between two builds of the same input goes in here -- no build
        // time, no machine name. The catalog is deterministic and that is worth keeping: it is
        // how a change to this exporter is checked, by rebuilding and comparing.
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "INSERT OR REPLACE INTO catalog_meta (key, value) VALUES ('schema_version', @v)";
            cmd.Parameters.AddWithValue("@v", CatalogSchemaVersion.ToString(CultureInfo.InvariantCulture));
            cmd.ExecuteNonQuery();
            Console.WriteLine($"Catalog schema version {CatalogSchemaVersion}.");
        }

        using (var cmd = connection.CreateCommand())
        {
            // Write-ahead logging is for the build; the finished catalog is only ever read.
            // Leaving it on would make every reader create a -wal and a -shm next to the file
            // in the user's configuration directory, and would stop it being read at all from
            // somewhere not writable. Switching back checkpoints the log and removes both.
            cmd.CommandText = "PRAGMA journal_mode = DELETE; PRAGMA optimize; VACUUM;";
            cmd.ExecuteNonQuery();
        }

        connection.Close();
        var fileInfo = new FileInfo(dbPath);
        Console.WriteLine($"SQLite Export completed successfully! Database size: {fileInfo.Length / 1024} KB at {dbPath}");

        // This file is the deliverable, as it is. It was compressed at one point -- LZMA takes
        // the 253-controller catalog from 14.3 MB to 1.4 MB -- back when it was downloaded with
        // the integration. It is now built by the user and uploaded from the same machine, so
        // the transfer costs nothing and an archive would only add an unpacking step before
        // anything could be read. A plain SQLite file is opened and queried in place.
    }

    /// <summary>
    /// Print every controller the catalog can be built for, so that someone who knows what is
    /// on their wall can find the System ID to build for. Without this the single-controller
    /// build would be a guessing game, and the way out of it would be to build everything.
    /// </summary>
    public sealed record ControllerInfo(string SystemId, string Model, string Description);

    public static void ListDevices(VDataBase vDb, string culture) => PrintControllers(Controllers(vDb, culture));

    /// <summary>Every controller the catalog can be built for, with the description in one language.</summary>
    public static List<ControllerInfo> Controllers(VDataBase vDb, string culture)
    {
        // The language file parses while the controllers are selected.
        var textsRead = Task.Run(() => TextsFor(culture));
        var resultClassNames = vDb.EcnTableExtensions.AsNoTracking().ToDictionary(a => a.Id, a => a.Label);
        var devices = SelectDataPointTypes(vDb, new List<string> { "all" }, resultClassNames);
        var texts = textsRead.GetAwaiter().GetResult().ByLabel;

        string Describe(string? key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (texts.TryGetValue(key, out var v) || texts.TryGetValue(key.TrimStart('@'), out v)
                || texts.TryGetValue("@@" + key, out v))
                return v;
            return key;
        }

        var result = new List<ControllerInfo>();
        foreach (var device in devices)
        {
            // The description is a sentence with a label in front of it; the model code after
            // it is what identifies the variant.
            var text = Describe(device.DataPointType.Description);
            var colon = text.IndexOf(':');
            if (colon >= 0) text = text.Substring(colon + 1).Trim();
            result.Add(new ControllerInfo(device.Identification, device.DataPointType.Address ?? "", text));
        }
        return result;
    }

    public static void PrintControllers(IEnumerable<ControllerInfo> controllers)
    {
        var list = controllers.ToList();
        Console.WriteLine($"{list.Count} controllers, grouped by system id.");
        Console.WriteLine();
        foreach (var byId in list.GroupBy(d => d.SystemId).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            Console.WriteLine(byId.Key);
            foreach (var device in byId.OrderBy(d => d.Model, StringComparer.Ordinal))
                Console.WriteLine($"    {device.Model,-24} {device.Description}");
        }
        Console.WriteLine();
        Console.WriteLine("Several controllers can share one system id; building for it covers all of them.");
    }

    /// <summary>
    /// The structure of the catalog this tool writes, as one number.
    ///
    /// The catalog is built by each user and the integration that reads it is updated
    /// separately, so the two can be out of step in either direction. The integration checks
    /// this number against the one it needs and says which side is behind, instead of failing
    /// somewhere deep in a query on a column that is not there.
    ///
    /// Bump it whenever a change would make an older catalog wrong or unreadable: a table or
    /// column the reader needs, a changed meaning of an existing one, a different key format.
    /// Adding something the reader does not require yet does not need a bump.
    ///
    ///   1  first versioned catalog: level-name key stems on datapoint_defs, catalog_meta
    /// </summary>
    public const int CatalogSchemaVersion = 1;

    /// <summary>
    /// The MappingType values that mark a weekly programme, and the name the source files its
    /// level texts under. This map is the only place the two are connected, and it lives here
    /// rather than in the consumer: naming inside the source is this tool's business, and what
    /// the catalog hands on is a finished text key.
    ///
    /// The values that are not here are either not programmes at all (0) or a different kind of
    /// structure entirely (3 and 4 are fault-history buffers), and get no stem.
    /// </summary>
    private static readonly Dictionary<int, string> ScheduleLevelTypes = new()
    {
        [1] = "PhaseCircuitTime",
        [2] = "HydroExtractorCircuitTime",
        [5] = "HydroExtractorPhaseCircuitTime",
        [6] = "HydroExtractorPhaseCircuitTimeWW",
        [7] = "HydroExtractorPhaseCircuitTimeZP",
        [8] = "HydroExtractorPhaseCircuitTimeBuffer",
        [9] = "PhaseCircuitTimeFT",
        [10] = "HydroExtractorPhaseCircuitTimeLF",
    };

    /// <summary>
    /// Both text-key stems for a weekly programme's level names: the one shared by every
    /// programme of this type, and the one filed under this datapoint in particular. Append
    /// ".&lt;level&gt;" to either to get a key. The datapoint-specific one is written whether or
    /// not any text exists under it, because a lookup that misses simply falls through to the
    /// shared one: most specific first, general last.
    /// </summary>
    private static (string?, string?) ScheduleLevelStems(int mappingType, string eventTypeName, string address)
    {
        if (!ScheduleLevelTypes.TryGetValue(mappingType, out var typeName)) return (null, null);
        var stem = $"viessmann.CircuitTimes.{typeName}.Value";
        // The bare datapoint name: the source prefixes it with its own text-key namespace, and
        // the level texts are filed under the bare name with the address appended.
        var bare = eventTypeName.Split('~').Last().Split('.').Last();
        return (stem, $"viessmann.CircuitTimes.{typeName}.{bare}~{address}.Value");
    }
}
