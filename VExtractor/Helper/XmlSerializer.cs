using System.Xml;
using System.Xml.Serialization;

namespace VExtractor.Helper;

public class XmlDeserializer<T>
{
    public T? ReadData(string path)
    {

        if (string.IsNullOrEmpty(path)) return default;

        var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var xmlTextReader = new XmlTextReader(input);
        try
        {
            var xmlSerializer = new XmlSerializer(typeof(T));
            return (T?)xmlSerializer.Deserialize(xmlTextReader);
        }
        finally
        {
            input.Close();
        }
    }
}