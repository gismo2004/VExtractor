using YamlDotNet.Serialization;

namespace VExtractor.Models.Results;

public class OptoLinkExport
{
    [YamlMember(Alias = "binary_sensor")] public List<OptoBinarySensor> BinarySensor { get; set; }
    [YamlMember(Alias = "switch")] public List<OptoSwitch> Switch { get; set; }
    [YamlMember(Alias = "text_sensor")] public List<OptoTextSensor> TextSensor { get; set; }
    [YamlMember(Alias = "select")] public List<OptoSelect> Select { get; set; }
    [YamlMember(Alias = "sensor")] public List<OptoSensor> Sensor { get; set; }
    [YamlMember(Alias = "number")] public List<OptoNumber> Number { get; set; }
    [YamlMember(Alias = "optolink", Order = int.MinValue)] public Optolink Optolink { get; set; }
}

public class BasicProperties
{
    public BasicProperties(ResultValues resVal)
    {
        Id = resVal.Name;
        Address = resVal.Address;
        Name = resVal.PrettyName;
        UpdateInterval = resVal.Priority.ToString();
    }

    [YamlMember(Alias = "platform", Order = int.MinValue)] public string Platform => "optolink";
    [YamlMember(Alias = "id" , Order = int.MinValue+1)] public string Id { get; set; }
    [YamlMember(Alias = "name", Order = int.MinValue + 2)] public string Name { get; set; }
    [YamlMember(Alias = "address", Order = int.MinValue + 3)] public string Address { get; set; }
    [YamlMember(Alias = "update_interval", Order = int.MinValue+4)] public string? UpdateInterval { get; set; }

    public string GetDivRatioString(string? input)
    {
        if (input.StartsWith("Div"))
            return input.Replace("Div", "");

        return "0";
    }
}

public class OptoBinarySensor : BasicProperties
{
    public OptoBinarySensor(ResultValues resVal) : base(resVal)
    {
    }
}

public class OptoSwitch : BasicProperties
{
    public OptoSwitch(ResultValues resVal) : base(resVal)
    {
    }
}

public class OptoTextSensor : BasicProperties
{
    public OptoTextSensor(ResultValues resVal) : base(resVal)
    {
        Bytes = resVal.ByteLength;

        if (resVal.EnumValues.Count > 0)
        {
            Filters = new TextSensorFilters()
            {
                Map = resVal.EnumValues.Select(pair => $"{pair.Key} -> {pair.Value}").ToArray()
            };
        }

    }

    [YamlMember(Alias = "bytes")] public string? Bytes { get; set; }
    [YamlMember(Alias = "raw")] public bool? Raw { get; set; }
    [YamlMember(Alias = "icon")] public string? Icon { get; set; }
    [YamlMember(Alias = "filters")] public TextSensorFilters? Filters { get; set; }
}

public class OptoSelect : BasicProperties
{
    public OptoSelect(ResultValues resVal) : base(resVal)
    {
        Bytes = resVal.ByteLength;
        Map = resVal.EnumValues.Select(pair => $"{pair.Key} -> {pair.Value}").ToArray();
    }

    [YamlMember(Alias = "bytes")] public string? Bytes { get; set; }
    [YamlMember(Alias = "map")] public string[]? Map { get; set; }
}

public class OptoSensor : BasicProperties
{
    public OptoSensor(ResultValues resVal) : base(resVal)
    {
        DivRatio = GetDivRatioString(resVal.Conversion);
        Bytes = resVal.ByteLength;

        if (!string.IsNullOrWhiteSpace(resVal.Unit))
            UnitOfMeasurement = resVal.Unit;

        if ((bool)resVal.Unit?.Equals("°C"))
        {

            DeviceClass = "temperature";
            Icon = "mdi:thermometer-water";
        }
    }

    [YamlMember(Alias = "bytes")] public string? Bytes { get; set; }
    [YamlMember(Alias = "div_ratio")] public string? DivRatio { get; set; }
    [YamlMember(Alias = "unit_of_measurement")] public string? UnitOfMeasurement { get; set; }
    [YamlMember(Alias = "device_class")] public string? DeviceClass { get; set; }
    [YamlMember(Alias = "accuracy_decimals")] public string? AccuracyDecimals { get; set; }
    [YamlMember(Alias = "icon")] public string? Icon { get; set; }
}

public class OptoNumber : BasicProperties
{
    public OptoNumber(ResultValues resVal) : base(resVal)
    {
        MinValue = resVal.LowerLimit;
        MaxValue = resVal.UpperLimit;
        DivRatio = GetDivRatioString(resVal.Conversion);
        Bytes = resVal.ByteLength;

        if (resVal.Stepping != 0)
        {
            Step = resVal.Stepping;
        }

        if(!string.IsNullOrWhiteSpace(resVal.Unit))
            UnitOfMeasurement = resVal.Unit;

        if ((bool)resVal.Unit?.Equals("°C"))
        {
            DeviceClass = "temperature";
            Icon = "mdi:thermometer-water";
        }
    }

    [YamlMember(Alias = "unit_of_measurement")] public string? UnitOfMeasurement { get; set; }
    [YamlMember(Alias = "bytes")] public string? Bytes { get; set; }
    [YamlMember(Alias = "div_ratio")] public string? DivRatio { get; set; }
    [YamlMember(Alias = "min_value")] public double? MinValue { get; set; }
    [YamlMember(Alias = "max_value")] public double? MaxValue { get; set; }
    [YamlMember(Alias = "step")] public double? Step { get; set; }
    [YamlMember(Alias = "mode")] public string? Mode { get; set; }
    [YamlMember(Alias = "icon")] public string? Icon { get; set; }
    [YamlMember(Alias = "device_class")] public string? DeviceClass { get; set; }
}


public class Optolink
{
    [YamlMember(Alias = "tx_pin", Description = "'TX' is the default TX pin - change it if needed")]
    public string TxPin => "TX";

    [YamlMember(Alias = "rx_pin", Description = "'RX' is the default RX pin - change it if needed")]
    public string RxPin => "RX";

    [YamlMember(Alias = "protocol", Description = "Can be set to 'P300' or 'KW'")]
    public string Protocol => "P300";

    [YamlMember(Alias = "logger", Description = "Should be set to 'false' if not needed")]
    public string Logger => "false";

    [YamlMember(Alias = "device_info")] 
    public string DeviceInfo { get; set; }
}

public class TextSensorFilters
{
    [YamlMember(Alias = "map")] public string[]? Map { get; set; }
}