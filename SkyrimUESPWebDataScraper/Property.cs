using System.Xml.Serialization;

namespace Skyrim_Alchemy_Utility
{
    public class Property
    {
        [XmlAttribute]
        public string EfID;
        [XmlAttribute]
        public string IngID;
        public string PK
        {
            get { return (IngID == null ? "" : IngID) + (EfID == null ? "" : EfID); }
        }
    }
}