namespace VExtractor.Models.DataBase;

public class AdditionalData
{
    public string SDKDataType { get; set; }

    public string Option { get; set; }

    public string Address { get; set; }

    public string VitocomChannelID { get; set; }

    public string FCRead { get; set; }

    public string PrefixRead { get; set; }

    public string FCWrite { get; set; }

    public string PrefixWrite { get; set; }

    public string Parameter { get; set; }

    public string BlockLength { get; set; }

    public string BytePosition { get; set; }

    public string ByteLength { get; set; }

    public string BitPosition { get; set; }

    public string BitLength { get; set; }

    public string ConversionFactor { get; set; }

    public string ConversionOffset { get; set; }

    public string DefinitionType { get; set; }

    public string BlockFactor { get; set; }

    // Extension "MappingType" on ecnEventType (label viessmann.CircuitTimes.TableExtensions
    // .MappingField, hence the property name). Classifies the block datapoints whose bytes
    // are not a value but a structure: 1/9 phase schedule (start/end pairs), 2 quarter-hour
    // level bitmap, 5..8/10 phase schedule with a level byte (HK, WW, ZP, buffer, ventilation),
    // 3/4 error history. 0 means none.
    public string MappingField { get; set; }

    public string FunctionValue { get; set; }

    public string RPCHandler { get; set; }

    public string IsVD100 { get; set; }

    public string DPT_typ { get; set; }

    public string DPT_sub { get; set; }

    public string Flags { get; set; }

    public string TriggerMode { get; set; }

    public string TriggerModeParameterT { get; set; }

    public string TriggerModeParameterN { get; set; }

    public string History { get; set; }
}