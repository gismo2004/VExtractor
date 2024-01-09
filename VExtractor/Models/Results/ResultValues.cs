using System.ComponentModel;

namespace VExtractor.Models.Results;

public class ResultValues
{
    public string? Name { get; set; }

    public string? Address { get; set; }

    public string? PrettyName { get; set; }

    public string? Description { get; set; }

    public string? FCWrite { get; set; }

    public string? FCRead { get; set; }

    public string? Unit { get; set; }

    public string? Conversion { get; set; }

    public double? UpperLimit { get; set; }

    public double? LowerLimit { get; set; }

    public double? Stepping { get; set; }

    public int? Priority { get; set; }

    public string? ParameterType { get; set; }

    public string? DefaultValue { get; set; }

    public string? BitStartPos { get; set; }

    public string? BitLengh { get; set; }

    public string? BlockLength { get; set; }

    public string? ByteLength { get; set; }

    public string? BytePosition { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? SDKDataType { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? ConversionFactor { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? ConversionOffset { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? Flags { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<KeyValuePair<int, string>> EnumValues { get; set; } = new();

    public string Enumerations
    {
        get { return EnumValues.Aggregate("", (current, item) => current + item.Key + "," + item.Value + "|"); }
    }
}