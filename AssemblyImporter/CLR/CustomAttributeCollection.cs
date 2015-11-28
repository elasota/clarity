using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssemblyImporter.CLR
{
    public class CustomAttributeCollection : IEnumerable<CLRCustomAttributeRow>
    {
        private List<CLRCustomAttributeRow> m_customAttribs = new List<CLRCustomAttributeRow>();

        public void Add(CLRCustomAttributeRow ca)
        {
            m_customAttribs.Add(ca);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return m_customAttribs.GetEnumerator();
        }

        public IEnumerator<CLRCustomAttributeRow> GetEnumerator()
        {
            return m_customAttribs.GetEnumerator();
        }

        public static CustomAttributeCollection LazyCreate(ref CustomAttributeCollection existingCA)
        {
            if (existingCA == null)
                existingCA = new CustomAttributeCollection();
            return existingCA;
        }
    }
}
