using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssemblyImporter.TCLR
{
    public class TCLREH
    {
        const ushort EH_Catch    = 0x0000;
        const ushort EH_CatchAll = 0x0001;
        const ushort EH_Finally  = 0x0002;
        const ushort EH_Filter   = 0x0003;

        ushort mode;

        bool m_usingClassToken;

        TCLRIndex m_classToken;     // TBL_TypeDef | TBL_TypeRef
        TCLROffset m_filterStart;

        TCLRIndex classToken { get { return m_classToken; } set { m_classToken = value; m_usingClassToken = true; } }
        TCLROffset filterStart { get { return m_filterStart; } set { m_filterStart = value; m_usingClassToken = false; } }

        TCLROffset tryStart;
        TCLROffset tryEnd;
        TCLROffset handlerStart;
        TCLROffset handlerEnd;
    }
}
