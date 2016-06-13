using System;
using System.Collections.Generic;
using System.IO;
using Clarity.Rpa;

namespace Clarity.RpaCompiler.Instructions
{
    public class RloTerminateRoutesInstruction : HighInstruction, IBranchingInstruction
    {
        public struct RouteTermination
        {
            private int m_routeID;
            private HighCfgNode m_successor;

            public int RouteID { get { return m_routeID; } }
            public HighCfgNode Successor { get { return m_successor; } }

            public RouteTermination(int routeID, HighCfgNode successor)
            {
                m_routeID = routeID;
                m_successor = successor;
            }
        }

        public struct EdgedRouteTermination
        {
            private int m_routeID;
            private HighCfgEdge m_successor;

            public int RouteID { get { return m_routeID; } }
            public HighCfgEdge Successor { get { return m_successor; } }

            public EdgedRouteTermination(int routeID, HighCfgEdge successor)
            {
                m_routeID = routeID;
                m_successor = successor;
            }

            public void VisitSuccessors(VisitCfgEdgeDelegate visitor)
            {
                visitor(ref m_successor);
            }
        }

        private EdgedRouteTermination[] m_routeTerminations;
        private HighCfgEdge m_nextFinally;  // Optional: Route to follow with next finally
        private HighSsaRegister m_exception;
        private HighSsaRegister m_route;

        public EdgedRouteTermination[] RouteTerminations { get { return m_routeTerminations; } }
        public HighCfgEdge NextFinally { get { return m_nextFinally; } }

        public RloTerminateRoutesInstruction()
        {
        }

        public RloTerminateRoutesInstruction(CodeLocationTag codeLocation, HighSsaRegister exceptionSrc, HighSsaRegister routeSrc, RouteTermination[] routeTerminations)
            : base(codeLocation)
        {
            List<EdgedRouteTermination> terminations = new List<EdgedRouteTermination>();
            foreach (RouteTermination termination in routeTerminations)
                terminations.Add(new EdgedRouteTermination(termination.RouteID, new HighCfgEdge(this, new HighCfgNodeHandle(termination.Successor))));
            m_routeTerminations = terminations.ToArray();
            m_exception = exceptionSrc;
            m_route = routeSrc;
        }

        public override Opcodes Opcode { get { return Opcodes.RloTerminateRoutes; } }

        void IBranchingInstruction.VisitSuccessors(VisitCfgEdgeDelegate visitor)
        {
            EdgedRouteTermination[] routeTerminations = m_routeTerminations;
            for (int i = 0; i < routeTerminations.Length; i++)
                routeTerminations[i].VisitSuccessors(visitor);
            visitor(ref m_nextFinally);
        }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
            visitor(ref m_exception);
            visitor(ref m_route);
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            throw new NotImplementedException();
        }

        protected override void WriteDisassemblyImpl(CfgWriter cw, DisassemblyWriter dw)
        {
            dw.Write(m_routeTerminations.Length.ToString());

            foreach (EdgedRouteTermination term in m_routeTerminations)
            {
                dw.Write(" ");
                dw.Write(term.RouteID.ToString());
            }
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            throw new NotImplementedException();
        }

        protected override HighInstruction CloneImpl()
        {
            List<RouteTermination> terminations = new List<RouteTermination>();
            foreach (EdgedRouteTermination termination in m_routeTerminations)
                terminations.Add(new RouteTermination(termination.RouteID, termination.Successor.Dest.Value));
            return new RloTerminateRoutesInstruction(this.CodeLocation, m_exception, m_route, terminations.ToArray());
        }

        public override bool MayThrow { get { return true; } }
        public override bool TerminatesControlFlow { get { return true; } }
    }
}
