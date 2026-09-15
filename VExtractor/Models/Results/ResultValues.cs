using System.ComponentModel;

namespace VExtractor.Models.Results;

public class ResultValues
{
    public int EventTypeId { get; set; }

    /// <summary>ecnEventType.Type: 1 = read-only, 2 = Remote_Procedure_Call (action), 3 = writable.
    /// Authoritative entity-kind classifier, better than inferring from FCWrite + enum count.</summary>
    public int EntityKind { get; set; }

    /// <summary>Tab-tree root branch (Overview/Statistic/Trending/Installation/Expertlayer/...),
    /// or null if this datapoint never appears in the catalog's UI tree (protocol-internal).</summary>
    public string? Tier { get; set; }

    /// <summary>Circuit-tree membership (HC1/HC2/HC3/Solar/WW), or null if not circuit-scoped.</summary>
    public string? Circuit { get; set; }

    /// <summary>Every ecnEventTypeGroup this datapoint belongs to, for matching against
    /// DisplayConditionRule.TargetGroupId at runtime (a group-level hide rule can target any of
    /// them, not just the tab/circuit-tree ones already summarised above).</summary>
    public List<int> GroupIds { get; set; } = new();

    /// <summary>Third '~'-segment of every group this datapoint belongs to (e.g. "WP",
    /// "Heizkreis1", "Anlagenuebersicht", "Warmwasser") -- what the datapoint is actually about,
    /// independent of which tab happens to show it.</summary>
    public List<string> Subjects { get; set; } = new();

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