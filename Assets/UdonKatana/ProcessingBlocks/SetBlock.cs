using System;
using System.Collections.Generic;
using JLChnToZ.VRC.UdonLowLevel;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    [ProcessingBlockPriority(Priority = 0)]
    internal class SetBlock: ProcessingBlock {
        public SetBlock(Node current, AssemblerState state)
            : base(current, state) {}

        protected override bool ResolveBlockType(out Type type) {
            if (Convert.ToString(current) == "=" && current.Count == 2) {
                type = state.builder.TryGetVariable(Convert.ToString(current[0]), out var def) ? def.type : typeof(object);
                return true;
            }
            type = null;
            return false;
        }
        
        protected override bool Process(Stack<ProcessingBlock> stack) {
            switch (i) {
                case 0:
                    result = new VariableName(Convert.ToString(current[0]));
                    stack.Push(Create(current[1], state, result));
                    i++;
                    return false;
                case 1:
                    if (ExplicitTarget.IsValid)
                        state.builder.EmitCopy(new VariableName(Convert.ToString(current[0])), ExplicitTarget);
                    ReturnAllTempVariables();
                    i++;
                    return true;
                default:
                    ReturnAllTempVariables();
                    return true;
            }
        }
    }
}