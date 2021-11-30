using System;
using System.Collections.Generic;
using JLChnToZ.VRC.UdonLowLevel;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    [ProcessingBlockPriority(Priority = 1000)]
    internal class NoTagBlock: ProcessingBlock {
        public NoTagBlock(Node current, AssemblerState state, VariableName explicitTarget = default)
            : base(current, state, explicitTarget) {}

        protected override bool BeforeResolveBlockType() {
            if (current.Tag != null) return true;
            if (current.Count == 1 && current[0].Count == 0)
                return false;
            return true;
        }

        protected override bool ResolveBlockType(out Type type) {
            if (current.Tag == null && current.Count > 0 && state.tagTypeMapping.TryGetValue(current[current.Count - 1], out type))
                return true;
            type = null;
            return false;
        }

        protected override bool Process(Stack<ProcessingBlock> stack) {
            if (i >= current.Count) return true;
            stack.Push(Create(current[i], state, i == current.Count - 1 ? ExplicitTarget : default));
            i++;
            return false;
        }
    }
}

