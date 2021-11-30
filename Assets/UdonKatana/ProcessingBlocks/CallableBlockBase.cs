using JLChnToZ.VRC.UdonLowLevel;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    internal abstract class CallableBlockBase: ProcessingBlock {
        protected readonly Node contentNode;

        protected CallableBlockBase(Node current, AssemblerState state, VariableName explicitTarget = default)
            : base(current, state, explicitTarget) {
            contentNode = current;
            while (contentNode.Tag == null && contentNode.Count == 1)
                contentNode = contentNode[0];
        }
    }
}