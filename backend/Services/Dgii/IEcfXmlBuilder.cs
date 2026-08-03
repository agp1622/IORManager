using System.Xml.Linq;

namespace IORManager.Services.Dgii;

public interface IEcfXmlBuilder
{
    /// <summary>Builds the unsigned e-CF XML document for the given data.</summary>
    XDocument Build(EcfDocumentData data);
}
