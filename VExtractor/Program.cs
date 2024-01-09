using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using VExtractor.Helper;
using VExtractor.Models.DataBase;
using VExtractor.Models.Results;
using YamlDotNet.Serialization;

var identifier = "2048";
var serializedIdentifier = DataHelper.Serialize(identifier);
var culture = "en";

using var vDb = new VDataBase();

//first call takes ~2s --> no solution found to speed this up
var resultClassNames = vDb.EcnTableExtensions.AsNoTracking().ToDictionary(a => a.Id, a => a.Label);


#region Translations

var cultureId = vDb.EcnCultures.AsNoTracking().FirstOrDefault(a => a.Name.Equals(culture));
var textResources = new XmlDeserializer<DocumentElement>().ReadData(DataHelper.GetTranslationFilePath("Textresource_" + culture + ".xml"))
    ?.TextResources.Where(a => a.CultureId == cultureId?.Id).ToList();

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
            .Where(ev => ev != null)
            .ToList()
    })
    .ToList();

#endregion

#region Fill result classes with values

var resultList = new List<ResultValues>();
var list = resultList;
Parallel.ForEach(finalList, evTypeDto =>
{
    if (evTypeDto.ExtensionValues == null) return;

    var additionalData = GetEventTypeAdditionalData(evTypeDto.ExtensionValues);

    if (string.IsNullOrWhiteSpace(additionalData.Address)) return;

    try
    {
        var resultValues = new ResultValues
        {
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

        list.Add(resultValues);
    }
    catch (Exception e)
    {
        Console.WriteLine(e);
    }
});

var binSensors = new List<OptoBinarySensor>();
var switches = new List<OptoSwitch>();
var textSensors = new List<OptoTextSensor>();
var selects = new List<OptoSelect>();
var sensors = new List<OptoSensor>();
var numbers = new List<OptoNumber>();

foreach (var resVal in resultList)
{
    if (resVal.FCWrite.Equals("undefined") && resVal.FCRead.Equals("Virtual_READ") && resVal.EnumValues.Count == 2)
    {
        binSensors.Add(new OptoBinarySensor(resVal));
        continue;
    }

    if (resVal.FCWrite.Equals("Virtual_WRITE") && resVal.FCRead.Equals("Virtual_READ") && resVal.EnumValues.Count == 2)
    {
        switches.Add(new OptoSwitch(resVal));
        continue;
    }

    if (resVal.FCWrite.Equals("undefined") && resVal.FCRead.Equals("Virtual_READ") && (resVal.ParameterType.ToLower().Equals("string") || resVal.EnumValues.Count > 2))
    {
        textSensors.Add(new OptoTextSensor(resVal));
        continue;
    }

    if (resVal.FCWrite.Equals("Virtual_WRITE") && resVal.FCRead.Equals("Virtual_READ") && resVal.EnumValues.Count > 2)
    {
        selects.Add(new OptoSelect(resVal));
        continue;
    }


    if (resVal.FCWrite.Equals("undefined") && resVal.FCRead.Equals("Virtual_READ"))
    {
        sensors.Add(new OptoSensor(resVal));
        continue;
    }

    if (resVal.FCWrite.Equals("Virtual_WRITE") && resVal.FCRead.Equals("Virtual_READ"))
    {
        numbers.Add(new OptoNumber(resVal));
        continue;
    }
}

var optoExport = new OptoLinkExport()
{
    BinarySensor = binSensors,
    Switch = switches,
    TextSensor = textSensors,
    Select = selects,
    Sensor = sensors,
    Number = numbers,
    Optolink = new Optolink()
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

//xlsx
var dataTable = DataHelper.ConvertToDataTable(resultList);
var wb = new XLWorkbook();
wb.Worksheets.Add(dataTable, "data");
wb.SaveAs(Path.Combine(resultPath, $"{filename}.xlsx"));

//json
var options = new JsonSerializerOptions { WriteIndented = true };
var json = JsonSerializer.Serialize(resultList, options);
File.WriteAllText(Path.Combine(resultPath, $"{filename}.json"), json);

//yaml
using var writer = new StreamWriter(Path.Combine(resultPath, $"{filename}.yaml"));
var serializer = new SerializerBuilder().ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull).Build(); 
serializer.Serialize(writer, optoExport);

#endregion

return;

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