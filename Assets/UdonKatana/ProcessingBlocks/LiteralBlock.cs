using System;
using System.Collections.Generic;
using JLChnToZ.VRC.UdonLowLevel;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    [ProcessingBlockPriority(Priority = int.MinValue)]
    internal class LiteralBlock: ProcessingBlock {
        public LiteralBlock(Node current, AssemblerState state)
            : base(current, state) {}

        protected override bool ResolveBlockType(out Type type) {
            if (current.Count == 0) {
                type = current.Tag?.GetType() ?? typeof(object);
                return true;
            }
            type = null;
            return false;
        }
        
        protected override bool Process(Stack<ProcessingBlock> stack) {
            if (i == 0) {
                if (ExplicitTarget.IsValid)
                    state.builder.EmitCopy(current.Tag, ExplicitTarget);
                else
                    state.builder.EmitNop();
                i++;
            }
            return true;
        }

    }
}