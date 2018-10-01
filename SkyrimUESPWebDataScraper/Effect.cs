using System.Xml.Serialization;

namespace Skyrim_Alchemy_Utility
{
    public class Effect
    {
        [XmlAttribute]
        public string ID;
        [XmlAttribute]
        public string Name;
        [XmlAttribute]
        public string Description;
        [XmlAttribute]
        public float Cost;
        [XmlAttribute]
        public int Mag;
        [XmlAttribute]
        public int Dur;
        [XmlAttribute]
        public int Value;
        [XmlAttribute]
        public int IsBeneficial;
    }
}