using System.IO;

namespace Clarity.Rpa
{
    public class HighField
    {
        private string m_name;
        private TypeSpecTag m_type;

        public string Name { get { return m_name; } }
        public TypeSpecTag Type { get { return m_type; } }

        public HighField(string name, TypeSpecTag type)
        {
            m_name = name;
            m_type = type;
        }

        public static HighField Read(TagRepository rpa, CatalogReader catalog, BinaryReader reader)
        {
            TypeSpecTag type = catalog.GetTypeSpec(reader.ReadUInt32());
            string name = catalog.GetString(reader.ReadUInt32());

            return new HighField(name, type);
        }

        public HighField Instantiate(TagRepository repo, TypeSpecTag[] argTypes)
        {
            TypeSpecTag newType = m_type.Instantiate(repo, argTypes);
            return new HighField(m_name, newType);
        }
    }
}
