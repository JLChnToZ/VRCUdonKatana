using System;
using System.Collections.Generic;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    [ProcessingBlockPriority(Priority = 0)]
    internal class TypeOfBlock: ProcessingBlock {
        public TypeOfBlock(Node current, AssemblerState state)
            : base(current, state) {}

        protected override bool BeforeResolveBlockType() => Convert.ToString(current) != "typeof";

        protected override bool ResolveBlockType(out Type type) {
            if (Convert.ToString(current) == "typeof") {
                type = typeof(Type);
                return true;
            }
            type = null;
            return false;
        }
        
        protected override bool Process(Stack<ProcessingBlock> stack) {
            var tag = Convert.ToString(current[0]);
            if (ExplicitTarget.IsValid && AssemblerStateHelper.typeDefs.TryGetValue(tag, out var type))
                state.builder.EmitCopy(type, ExplicitTarget);
            return true;
        }
    }
}