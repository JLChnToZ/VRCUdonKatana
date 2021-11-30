using System;
using System.Collections.Generic;
using JLChnToZ.VRC.UdonLowLevel;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    [ProcessingBlockPriority(Priority = int.MinValue)]
    internal class NopBlock: ProcessingBlock {
        public NopBlock(Node current, AssemblerState state, VariableName explicitTarget = default)
            : base(current, state, explicitTarget) {}

        protected override bool ResolveBlockType(out Type type) {
            if (current == null && current.Count == 0) {
                type = typeof(object);
                return true;
            }
            type = null;
            return false;
        }
        
        protected override bool Process(Stack<ProcessingBlock> stack) {
            if (ExplicitTarget.IsValid)
                state.builder.EmitCopy(null, ExplicitTarget);
            else
                state.builder.EmitNop();
            ReturnAllTempVariables();
            return true;
        }
    }
}