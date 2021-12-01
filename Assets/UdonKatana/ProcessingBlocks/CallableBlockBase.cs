using JLChnToZ.VRC.UdonLowLevel;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    internal abstract class CallableBlockBase: ProcessingBlock {
        protected readonly bool isQuotedNoArgNode;

        protected CallableBlockBase(Node current, AssemblerState state)
            : base(current, state) {
            isQuotedNoArgNode = NoTagBlock.IsNoArgsNode(current);
        }
    }
}