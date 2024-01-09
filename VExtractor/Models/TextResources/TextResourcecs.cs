using System.Xml.Serialization;

[XmlRoot(ElementName = "DocumentElement", Namespace = "http://viessmann.com/Textresource.xsd")]
public class DocumentElement
{
    [XmlArray("DefaultCultures")]
    [XmlArrayItem("DefaultCulture")]
    public List<DefaultCulture> DefaultCultures { get; set; }

    [XmlArray("Cultures")]
    [XmlArrayItem("Culture")]
    public List<Culture> Cultures { get; set; }

    [XmlArray("TextResources")]
    [XmlArrayItem("TextResource")]
    public List<TextResource> TextResources { get; set; }
}

public class DefaultCulture
{
    [XmlAttribute("CompanyId")] public int CompanyId { get; set; }

    [XmlAttribute("DefaultCultureId")] public int DefaultCultureId { get; set; }
}

public class Culture
{
    [XmlAttribute("Id")] public int Id { get; set; }

    [XmlAttribute("CompanyId")] public int CompanyId { get; set; }

    [XmlAttribute("Name")] public string Name { get; set; }
}

public class TextResource
{
    [XmlAttribute("CompanyId")] public int CompanyId { get; set; }

    [XmlAttribute("CultureId")] public int CultureId { get; set; }

    [XmlAttribute("Label")] public string Label { get; set; }

    [XmlAttribute("Value")] public string Value { get; set; }
}