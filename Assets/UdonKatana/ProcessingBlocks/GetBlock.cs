using System;
using System.Collections.Generic;
using JLChnToZ.VRC.UdonLowLevel;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    [ProcessingBlockPriority(Priority = 0)]
    internal class GetBlock: ProcessingBlock {
        public GetBlock(Node current, AssemblerState state, VariableName explicitTarget = default)
            : base(current, state, explicitTarget) {}

        protected override bool BeforeResolveBlockType() => Convert.ToString(current) != "$" || current.Count != 1;

        protected override bool ResolveBlockType(out Type type) {
            if (Convert.ToString(current) == "$" && current.Count == 1) {
                type = state.builder.TryGetVariable(Convert.ToString(current[0]), out var varDef) ? varDef.type : null;
                return true;
            }
            type = null;
            return false;
        }

        protected override bool Process(Stack<ProcessingBlock> stack) {
            if (i == 0 && ExplicitTarget.IsValid)
                state.builder.EmitCopy(new VariableName(Convert.ToString(current[0])), ExplicitTarget);
            i++;
            return true;
        }
    }
}