﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Clarity.Rpa
{
    public interface ILocalUsingInstruction
    {
        void VisitLocalRefs(HighInstruction.VisitLocalDelegate visitor);
    }
}
