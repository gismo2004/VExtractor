namespace VExtractor.Models.DataBase;

public class EcnEventTypeDto
{
    public EcnEventType EventType { get; set; }

    public List<EcnTableExtensionValue>? ExtensionValues { get; set; }

    public List<EcnEventValueType?> EventValuesTypes { get; set; }
}