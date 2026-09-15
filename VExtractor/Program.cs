using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using VExtractor.Helper;
using VExtractor.Models.DataBase;
using VExtractor.Models.Results;
using YamlDotNet.Serialization;
using VExtractor;

if (args.Length == 0)
    return Guided.Run();

if (args[0] is "-h" or "--help" or "help")
{
    Console.WriteLine("VExtractor: builds the controller catalog for the OptoV integration.");
    Console.WriteLine();
    Console.WriteLine("Run it without arguments for a guided build that finds what it needs and asks the rest.");
    Console.WriteLine();
    Console.WriteLine("  VExtractor prepare <Setup.exe> <dir> [languages]   unpack definitions and texts");
    Console.WriteLine("  VExtractor devices [language]                    list controllers and system ids");
    Console.WriteLine("  VExtractor sqlite <out.db> <ids|all> [languages] build the catalog");
    Console.WriteLine("  VExtractor load <DPDefinitions.xml> <source.db>  definitions -> source database");
    Console.WriteLine();
    Console.WriteLine("VEXTRACTOR_SOURCE_DIR and VEXTRACTOR_SOURCE_DB say where prepare put things.");
    return 0;
}

var identifier = args[0];
var serializedIdentifier = DataHelper.Serialize(identifier);
var culture = args.Length > 1 ? args[1] : "de";

// load <DPDefinitions.xml> <source.db>: turn the installer's definition file into the SQLite
// source the other commands read (with VEXTRACTOR_SOURCE_DB pointing at it). Needs no server.
if (args.Length > 0 && args[0].ToLowerInvariant() == "load")
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("usage: VExtractor load <DPDefinitions.xml> <source.db>");
        return 0;
    }
    SourceLoader.Load(args[1], args[2]);
    return 0;
}

// prepare <Setup.exe> <target dir> [languages]: take the definitions and the display texts out
// of the installer with 7-Zip. No installation, nothing run from inside it.
if (args.Length > 0 && args[0].ToLowerInvariant() == "prepare")
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("usage: VExtractor prepare <Setup.exe> <target dir> [languages]");
        return 0;
    }
    if (!File.Exists(args[1]))
    {
        Console.Error.WriteLine($"No such file: {args[1]}");
        return 1;
    }
    var languages = (args.Length > 3 && !string.IsNullOrWhiteSpace(args[3]) ? args[3] : "de,en")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var wantedFiles = new List<string> { "DPDefinitions.xml" };
    wantedFiles.AddRange(languages.Select(l => $"Textresource_{l}.xml"));

    List<string> extracted;
    try { extracted = InstallerReader.Extract(args[1], wantedFiles, args[2]); }
    catch (FileNotFoundException err)
    {
        Console.Error.WriteLine(err.Message);
        return 1;
    }
    var missing = wantedFiles.Where(w => !extracted.Contains(w, StringComparer.OrdinalIgnoreCase)).ToList();
    if (missing.Count > 0)
    {
        Console.Error.WriteLine($"Not in this installer: {string.Join(", ", missing)}");
        return 1;
    }
    Console.WriteLine($"Ready in {args[2]}.");
    return 0;
}

// devices [culture]: what can be built, so the System ID for a build can be looked up by model.
if (args.Length > 0 && args[0].ToLowerInvariant() == "devices")
{
    using var listDb = new VDataBase();
    SqliteExporter.ListDevices(listDb, args.Length > 1 ? args[1] : "de");
    return 0;
}

Console.WriteLine($"Starting extraction for identifier: {identifier}, culture: {culture}");

using var vDb = new VDataBase();

if (args.Length > 0 && args[0].ToLowerInvariant() == "sqlite")
{
    var outputPath = args.Length > 1 ? args[1] : Path.Combine(Directory.GetCurrentDirectory(), "viessmann.db");
    // Pass the identifier argument through untouched: SqliteExporter.SelectDataPointTypes()
    // reads "default"/"all"/"optolink" as every Optolink-reachable controller and anything
    // else as an explicit System ID list. Expanding "default" to a hardcoded handful here
    // would silently cap the catalog at those few devices.
    var idList = args.Length > 2 && !string.IsNullOrEmpty(args[2])
        ? args[2].Split(',').Select(s => s.Trim()).ToList()
        : new List<string> { "all" };

    var allCultures = new List<string> { "de", "en", "fr", "it", "es", "nl", "pl", "da", "sv", "cs", "ru", "tr", "hu", "hr", "no", "sk", "ro", "lt" };
    var cultureList = args.Length > 3 && !string.IsNullOrEmpty(args[3])
        ? (args[3].ToLowerInvariant() is "all" or "default"
            ? allCultures
            : args[3].Split(',').Select(s => s.Trim()).ToList())
        : allCultures;

    SqliteExporter.Export(vDb, outputPath, idList, cultureList);
    return 0;
}

//first call takes ~2s --> no solution found to speed this up
var resultClassNames = vDb.EcnTableExtensions.AsNoTracking().ToDictionary(a => a.Id, a => a.Label);


#region Translations

var cultureId = vDb.EcnCultures.AsNoTracking().FirstOrDefault(a => a.Name.Equals(culture));
var textResources = new XmlDeserializer<DocumentElement>().ReadData(DataHelper.GetTranslationFilePath("Textresource_" + culture + ".xml"))
    ?.TextResources.Where(a => a.CultureId == cultureId?.Id).ToList() ?? new List<TextResource>();

var eventTypeTranslation = textResources.ToDictionary(a => a.Label, a => a.Value);
var unitTranslation = textResources.Where(a => a.Label.Contains("ecnUnit")).ToDictionary(a => a.Label, a => a.Value);

#endregion

#region Find DataPoint name

//this can match multiple times
//RefId == 6 --> label.tableextension.ecnDatapointType.Identification
var foundPkIds = vDb.EcnTableExtensionValues.AsNoTracking()
    .Where(a => a.InternalValue.Equals(serializedIdentifier) && a.RefId == 6).ToList();

var foundDataPoint = vDb.EcnDatapointTypes.AsNoTracking().ToList()
    .First(a => foundPkIds.Any(b => b.PkId == a.Id && b.PkCompanyId == a.CompanyId));

#endregion

#region Get all data for given DataPoint

var eventTypeIds = vDb.EcnDataPointTypeEventTypeLinks.AsNoTracking().Where(a => a.DataPointTypeId == foundDataPoint.Id)
    .Select(b => b.EventTypeId).ToList();

var eventTypeList = vDb.EcnEventTypes.AsNoTracking().Where(a => eventTypeIds.Contains(a.Id)).ToList();
var eventTypePkVal = eventTypeList.Select(a => string.Join(';', a.Id, a.CompanyId)).ToList();

var eventValueTypeLinks = vDb.EcnEventTypeEventValueTypeLinks.Where(a => eventTypeIds.Contains(a.EventTypeId)).ToList();
var eventValueTypeIds = eventValueTypeLinks.Select(a => a.EventValueId);
var eventValueTypes = vDb.EcnEventValueTypes.AsNoTracking().Where(ev => eventValueTypeIds.Contains(ev.Id)).ToList();
var tableExtensionValues = vDb.EcnTableExtensionValues.AsNoTracking().Where(a => eventTypePkVal.Contains(a.PkValue)).ToList();

#endregion

#region Tier / circuit / group membership

// A group's root address (that of its ancestor without a parent) ends in the tab-tree branch name
// (Overview/Statistic/Trending/...) UNLESS the root is the circuit tree (ecnsysEventTypeGroupHC~<model>),
// in which case the *leaf* group's own address ends in the circuit (HC1/HC2/HC3/Solar/WW).
// Scoped through the group's own DataPointTypeId, which is family-scoped; EventTypeIds are
// reused across families, so an unscoped join pulls in other families' groups.
// ecnEventTypeEventTypeGroupLink has no DataPointTypeId column, and EventTypeIds are reused
// across device families (confirmed: the same EventTypeId links to near-identical "Anlagenuebersicht"/
// "HC1" groups belonging to *other* families' own DataPointTypeIds, e.g. VBC702_S/VBC702_AW).
// Scope through ecnEventTypeGroup.DataPointTypeId, which IS family-scoped -- same fix as the
// display-condition group-matching below, just missed here on the first pass.
var fullGroups = vDb.EcnEventTypeGroups.AsNoTracking()
    .Where(g => g.DataPointTypeId == foundDataPoint.Id)
    .OrderBy(g => g.ParentId)
    .ThenBy(g => g.OrderIndex)
    .ToList();
var familyGroupIds = fullGroups.Select(g => g.Id).ToHashSet();
var familyGroupAddress = fullGroups.ToDictionary(g => g.Id, g => g.Address);

string TranslateGroup(EcnEventTypeGroup g)
{
    var groupName = TryTranslate(g.Description ?? string.Empty);
    if (string.IsNullOrEmpty(groupName) || groupName == g.Description)
        groupName = TryTranslate(g.Name ?? string.Empty);
    if (string.IsNullOrEmpty(groupName) || groupName == g.Name)
        groupName = TryTranslate("ecnsysEventTypeGroup~" + g.Address);
    if (string.IsNullOrEmpty(groupName) || groupName.StartsWith("ecnsysEventTypeGroup~"))
        groupName = TryTranslate("viessmann.eventtypegroup.name." + g.Address);
    if (string.IsNullOrEmpty(groupName) || groupName.StartsWith("viessmann.eventtypegroup.name."))
        groupName = g.Address.Split('~').Last();
    return groupName;
}

var groupDefinitions = fullGroups.Select(g => new GroupDefinition
{
    Id = g.Id,
    ParentId = g.ParentId == -1 ? null : g.ParentId,
    Address = g.Address,
    Name = TranslateGroup(g),
    OrderIndex = g.OrderIndex,
}).ToList();

string? CircuitOfGroupAddress(string? groupAddress)
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

var circuits = new Dictionary<string, string>();
foreach (var g in fullGroups.Where(g => g.Address.Contains("~Overview~")))
{
    var c = CircuitOfGroupAddress(g.Address);
    if (c != null && !circuits.ContainsKey(c))
        circuits[c] = TranslateGroup(g);
}
foreach (var g in fullGroups)
{
    var c = CircuitOfGroupAddress(g.Address);
    if (c != null && !circuits.ContainsKey(c))
        circuits[c] = TranslateGroup(g);
}

var groupLinks = vDb.EcnEventTypeEventTypeGroupLinks.AsNoTracking()
    .Where(l => eventTypeIds.Contains(l.EventTypeId) && familyGroupIds.Contains(l.EventTypeGroupId)).ToList();

var groupIdsByEventTypeId = groupLinks.GroupBy(l => l.EventTypeId)
    .ToDictionary(g => g.Key, g => g.Select(l => l.EventTypeGroupId).Distinct().ToList());

var circuitByEventTypeId = new Dictionary<int, string>();
foreach (var (etId, gIds) in groupIdsByEventTypeId)
{
    var matchedCircuits = gIds
        .Select(gid => familyGroupAddress.TryGetValue(gid, out var addr) ? CircuitOfGroupAddress(addr) : null)
        .Where(c => c != null)
        .Distinct()
        .ToList();

    if (matchedCircuits.Count == 1)
    {
        circuitByEventTypeId[etId] = matchedCircuits[0]!;
    }
}

// The same rule the catalog uses, from the groups' own chain of parents.
var tierByEventTypeId = SqliteExporter.TiersOf(groupIdsByEventTypeId, SqliteExporter.RootAddresses(vDb));

// The "subject" (what physical thing a group is actually about -- WP/Heizkreis1/Warmwasser/
// Anlagenuebersicht/...) is the third '~'-segment of a group's own address, e.g.
// "<model>~Overview~Heizkreis1" -> "Heizkreis1". Unlike Tier, a datapoint can legitimately have
// several subjects at once (it appears in "Overview~WP" AND "DiagnosisDiagnosis1~WP" etc) --
// kept as a set, not reduced to one, since a consumer needs to know ALL of them to decide
// relevance generically (see autoconfig.py).
string? SubjectOf(string? groupAddress)
{
    var parts = groupAddress?.Split('~');
    return parts != null && parts.Length >= 3 ? parts[2] : null;
}

var subjectsByEventTypeId = groupIdsByEventTypeId.ToDictionary(
    kv => kv.Key,
    kv => kv.Value
        .Select(gid => familyGroupAddress.TryGetValue(gid, out var addr) ? SubjectOf(addr) : null)
        .Where(s => s != null)
        .Cast<string>()
        .Distinct()
        .ToList());

#endregion

#region Display-condition rules -- exported unevaluated; a runtime probe step
// (not this extractor, which has no hardware connection) reads the ConditionEventTypeId
// addresses and decides what to hide.

var conditionGroups = vDb.EcnDisplayConditionGroups.AsNoTracking()
    .Where(g => (g.EventTypeIdDest != -1 && eventTypeIds.Contains(g.EventTypeIdDest))
             || (g.EventTypeGroupIdDest != -1 && familyGroupIds.Contains(g.EventTypeGroupIdDest)))
    .ToList();

var conditionGroupIds = conditionGroups.Select(g => g.Id).ToList();
var displayConditions = vDb.EcnDisplayConditions.AsNoTracking()
    .Where(c => conditionGroupIds.Contains(c.ConditionGroupId)).ToList();

var conditionValueIds = displayConditions.Select(c => c.EventTypeValueCondition).Distinct().ToList();
var conditionValueTypes = vDb.EcnEventValueTypes.AsNoTracking()
    .Where(v => conditionValueIds.Contains(v.Id)).ToList()
    .ToDictionary(v => v.Id, v => v);

var displayConditionRules = new List<DisplayConditionRule>();
foreach (var group in conditionGroups)
{
    var rule = new DisplayConditionRule
    {
        TargetEventTypeId = group.EventTypeIdDest == -1 ? null : group.EventTypeIdDest,
        TargetGroupId = group.EventTypeGroupIdDest == -1 ? null : group.EventTypeGroupIdDest,
        Logic = group.Type == 1 ? "AND" : "OR",
    };

    foreach (var cond in displayConditions.Where(c => c.ConditionGroupId == group.Id))
    {
        var op = cond.Condition switch { 0 => "eq", 1 => "ne", 2 => "gt", 4 => "lt", _ => "eq" };
        var compareValue = conditionValueTypes.TryGetValue(cond.EventTypeValueCondition, out var valueType)
            ? GetEnumKeyValuePair(valueType.EnumReplaceValue ?? string.Empty).Key
            : (int)(cond.ConditionValue ?? 0);

        rule.Conditions.Add(new DisplayCondition
        {
            ConditionEventTypeId = cond.EventTypeIdCondition,
            Operator = op,
            CompareValue = compareValue,
        });
    }

    displayConditionRules.Add(rule);
}

#endregion

#region Create a list with usefull data

var finalList = eventTypeList.Join(tableExtensionValues, a => new { a.Id, a.CompanyId },
        b => new { Id = int.Parse(b.PkValue.Split(';').First()), CompanyId = byte.Parse(b.PkValue.Split(';').Last()) },
        (a, b) => new { EventType = a, ExtensionValues = b })
    .GroupBy(x => x.EventType)
    .Select(groupResult => new EcnEventTypeDto
    {
        EventType = groupResult.Key,
        ExtensionValues = groupResult.Select(x => x.ExtensionValues).ToList(),
        EventValuesTypes = eventValueTypeLinks
            .Where(c => c.EventTypeId == groupResult.Key.Id)
            .Select(c => eventValueTypes.FirstOrDefault(ev => ev.Id == c.EventValueId))
            .OfType<EcnEventValueType>()
            .ToList()
    })
    .ToList();

#endregion

#region Fill result classes with values

// ConcurrentBag, not List<T> -- Parallel.ForEach below calls .Add from multiple threads, and
// List<T>.Add is not thread-safe (confirmed: two consecutive runs against the same unchanged
// database produced 1298 and 1299 items before this fix -- silent, non-deterministic data loss).
var resultBag = new System.Collections.Concurrent.ConcurrentBag<ResultValues>();
Parallel.ForEach(finalList, evTypeDto =>
{
    if (evTypeDto.ExtensionValues == null) return;

    var additionalData = GetEventTypeAdditionalData(evTypeDto.ExtensionValues);

    if (string.IsNullOrWhiteSpace(additionalData.Address)) return;

    try
    {
        var resultValues = new ResultValues
        {
            EventTypeId = evTypeDto.EventType.Id,
            EntityKind = evTypeDto.EventType.Type,
            Tier = tierByEventTypeId.TryGetValue(evTypeDto.EventType.Id, out var evTier) ? evTier : null,
            Circuit = circuitByEventTypeId.TryGetValue(evTypeDto.EventType.Id, out var evCircuit) ? evCircuit : null,
            GroupIds = groupIdsByEventTypeId.TryGetValue(evTypeDto.EventType.Id, out var evGroupIds) ? evGroupIds : new List<int>(),
            Subjects = subjectsByEventTypeId.TryGetValue(evTypeDto.EventType.Id, out var evSubjects) ? evSubjects : new List<string>(),
            Name = evTypeDto.EventType.Address?.Split('~').First().Split('.').Last(),
            Address = additionalData.Address,
            Description = TryTranslate(evTypeDto.EventType.Description),
            BitLengh = additionalData.BitLength,
            BitStartPos = additionalData.BitPosition,
            Priority = evTypeDto.EventType.Priority,
            Conversion = evTypeDto.EventType.Conversion,
            DefaultValue = evTypeDto.EventType.DefaultValue,
            LowerLimit = evTypeDto.EventValuesTypes?.Min(a => a.LowerBorder),
            UpperLimit = evTypeDto.EventValuesTypes?.Max(a => a.UpperBorder),
            PrettyName = TryTranslate(evTypeDto.EventType.Name).Split("~").Last(),
            FCRead = additionalData.FCRead,
            ParameterType = additionalData.Parameter,
            Unit = TryGetUnit(evTypeDto.EventValuesTypes?.FirstOrDefault()?.Unit ?? string.Empty),
            Stepping = evTypeDto.EventValuesTypes?.FirstOrDefault()?.Stepping ?? 0,
            ConversionFactor = additionalData.ConversionFactor,
            BlockLength = additionalData.BlockLength,
            ByteLength = additionalData.ByteLength,
            BytePosition = additionalData.BytePosition,
            FCWrite = additionalData.FCWrite,
            ConversionOffset = additionalData.ConversionOffset,
            Flags = additionalData.Flags,
            SDKDataType = additionalData.SDKDataType
        };

        if (evTypeDto.EventType.EnumType)
            resultValues.EnumValues.AddRange(
                evTypeDto.EventValuesTypes?.Select(newVal =>
                    GetEnumKeyValuePair(newVal?.EnumReplaceValue ?? string.Empty)) ??
                Array.Empty<KeyValuePair<int, string>>());

        resultBag.Add(resultValues);
    }
    catch (Exception e)
    {
        Console.WriteLine(e);
    }
});

var resultList = resultBag.ToList();

var binSensors = new List<OptoBinarySensor>();
var switches = new List<OptoSwitch>();
var textSensors = new List<OptoTextSensor>();
var selects = new List<OptoSelect>();
var sensors = new List<OptoSensor>();
var numbers = new List<OptoNumber>();

foreach (var resVal in resultList)
{
    if (resVal.FCWrite is "undefined" && resVal.FCRead is "Virtual_READ" && resVal.EnumValues.Count == 2)
    {
        binSensors.Add(new OptoBinarySensor(resVal));
        continue;
    }

    if (resVal.FCWrite is "Virtual_WRITE" && resVal.FCRead is "Virtual_READ" && resVal.EnumValues.Count == 2)
    {
        switches.Add(new OptoSwitch(resVal));
        continue;
    }

    if (resVal.FCWrite is "undefined" && resVal.FCRead is "Virtual_READ" && (string.Equals(resVal.ParameterType, "string", StringComparison.OrdinalIgnoreCase) || resVal.EnumValues.Count > 2))
    {
        textSensors.Add(new OptoTextSensor(resVal));
        continue;
    }

    if (resVal.FCWrite is "Virtual_WRITE" && resVal.FCRead is "Virtual_READ" && resVal.EnumValues.Count > 2)
    {
        selects.Add(new OptoSelect(resVal));
        continue;
    }

    if (resVal.FCWrite is "undefined" && resVal.FCRead is "Virtual_READ")
    {
        sensors.Add(new OptoSensor(resVal));
        continue;
    }

    if (resVal.FCWrite is "Virtual_WRITE" && resVal.FCRead is "Virtual_READ")
    {
        numbers.Add(new OptoNumber(resVal));
        continue;
    }
}

var optoExport = new OptoLinkExport
{
    BinarySensor = binSensors,
    Switch = switches,
    TextSensor = textSensors,
    Select = selects,
    Sensor = sensors,
    Number = numbers,
    Optolink = new Optolink
    {
        DeviceInfo = foundDataPoint.Description
    }
};
#endregion

#region Export Data

//prepare data
resultList = [.. resultList.OrderBy(a => a.Name)];
var resultPath = Path.Combine(DataHelper.GetResultDataPath(), $"{identifier}-{foundDataPoint.Address}");
var filename = $"{foundDataPoint.Address}_{culture}";
Directory.CreateDirectory(resultPath);

Console.WriteLine($"Exporting {resultList.Count} items to {resultPath}...");

//xlsx
var dataTable = DataHelper.ConvertToDataTable(resultList);
var wb = new XLWorkbook();
wb.Worksheets.Add(dataTable, "data");
wb.SaveAs(Path.Combine(resultPath, $"{filename}.xlsx"));

//json
var options = new JsonSerializerOptions { WriteIndented = true };
var json = JsonSerializer.Serialize(resultList, options);
File.WriteAllText(Path.Combine(resultPath, $"{filename}.json"), json);

//json, enriched -- separate file so existing consumers of the flat {filename}.json above
//(db_catalog.py, verify_datapoints.py, export_profiles.py) are untouched.
var enrichedExport = new EnrichedExport
{
    SystemId = identifier,
    DeviceModel = foundDataPoint.Address,
    DeviceName = foundDataPoint.Description?.Replace("Allgemeine Produktbeschreibung: ", "").Trim() ?? foundDataPoint.Address,
    Language = culture,
    Circuits = circuits,
    Groups = groupDefinitions,
    Datapoints = resultList,
    DisplayConditions = displayConditionRules,
};
var enrichedJson = JsonSerializer.Serialize(enrichedExport, options);
File.WriteAllText(Path.Combine(resultPath, $"{filename}_full.json"), enrichedJson);

//yaml
using (var writer = new StreamWriter(Path.Combine(resultPath, $"{filename}.yaml")))
{
    var serializer = new SerializerBuilder().ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull).Build();
    serializer.Serialize(writer, optoExport);
}

Console.WriteLine($"Export completed successfully! Files saved to: {resultPath}");

#endregion

return 0;
AdditionalData GetEventTypeAdditionalData(List<EcnTableExtensionValue> extensionValues)
{
    var result = new AdditionalData();
    foreach (var ecnTableExtensionValue in extensionValues)
    {
        _ = resultClassNames.TryGetValue(ecnTableExtensionValue.RefId, out var fieldName);
        fieldName = fieldName?.Split('.').Last();
        if (fieldName == null) continue;

        var property = typeof(AdditionalData).GetProperty(fieldName);
        if (property == null) continue;

        var convertedValue = Convert.ChangeType(DataHelper.Deserialize(ecnTableExtensionValue.InternalValue),
            property.PropertyType);
        property.SetValue(result, convertedValue);
    }

    return result;
}

KeyValuePair<int, string> GetEnumKeyValuePair(string input)
{
    _ = int.TryParse(input.Split("~").Last(), out var key);
    var value = TryTranslate(input);

    return new KeyValuePair<int, string>(key, value);
}

string TryGetUnit(string input)
{
    unitTranslation.TryGetValue(input, out var result);
    return result ?? string.Empty;
}

string TryTranslate(string input)
{
    eventTypeTranslation.TryGetValue(input.Replace("@@", ""), out var result);
    return result == null ? input : result.Replace("##ecntab##", "\t").Replace("##ecnnewline##", " - ");
}