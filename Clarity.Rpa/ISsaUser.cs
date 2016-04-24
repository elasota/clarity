using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Clarity.Rpa
{
    public interface ISsaUser
    {
        void VisitSsaUses(HighInstruction.VisitSsaDelegate visitor);
    }
}
