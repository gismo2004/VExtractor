namespace VExtractor.Models.Results;

/// <summary>Wrapper for the enriched export: the same datapoints as the plain flat catalog, plus
/// the unevaluated display-condition rules a runtime probe-and-evaluate step needs.</summary>
public class EnrichedExport
{
    public string SystemId { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public Dictionary<string, string> Circuits { get; set; } = new();
    public List<GroupDefinition> Groups { get; set; } = new();
    public List<ResultValues> Datapoints { get; set; } = new();
    public List<DisplayConditionRule> DisplayConditions { get; set; } = new();
}

public class GroupDefinition
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
}

/// <summary>
/// One resolved ecnDisplayConditionGroup + its child ecnDisplayCondition rows, in a form a
/// runtime probe-and-evaluate step can consume directly without touching the raw EAV/BinaryFormatter
/// encoding.
/// </summary>
public class DisplayConditionRule
{
    /// <summary>ecnEventType.Id of the single datapoint this rule hides, or null if this rule
    /// targets a whole group instead (see TargetGroupId).</summary>
    public int? TargetEventTypeId { get; set; }

    /// <summary>ecnEventTypeGroup.Id of the group this rule hides (hides every datapoint whose
    /// ResultValues.GroupIds contains this id), or null if this rule targets a single datapoint
    /// instead (see TargetEventTypeId). Exactly one of the two is set, per ecnDisplayConditionGroup's
    /// own EventTypeIdDest/EventTypeGroupIdDest pair (one of them is always -1 in the source data).</summary>
    public int? TargetGroupId { get; set; }

    /// <summary>"AND" (Type=1, hide only if every condition matches) or "OR" (Type=2, hide if any
    /// condition matches) -- verified against real hardware.</summary>
    public string Logic { get; set; } = "OR";

    public List<DisplayCondition> Conditions { get; set; } = new();
}

public class DisplayCondition
{
    /// <summary>ecnEventType.Id of the datapoint to probe at init time -- cross-reference against
    /// ResultValues.EventTypeId in the same export to find its Address.</summary>
    public int ConditionEventTypeId { get; set; }

    /// <summary>"eq" (Condition=0) or "ne" (Condition=1) -- the only operators seen on real hardware
    /// so far. "gt"/"lt" (Condition=2/4) are inferred from DB-wide data, not yet verified live.</summary>
    public string Operator { get; set; } = "eq";

    /// <summary>The value to compare the probed datapoint's decoded reading against. Resolved from
    /// ecnEventValueType.EnumReplaceValue the same way a normal enum value is (see
    /// Program.cs GetEnumKeyValuePair) -- these are enum keys, not raw register bytes.</summary>
    public int CompareValue { get; set; }
}
