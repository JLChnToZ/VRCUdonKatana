using System;
using System.Collections.Generic;
using JLChnToZ.VRC.UdonLowLevel;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    [ProcessingBlockPriority(Priority = 0)]
    internal class WhileBlock: ProcessingBlock {
        JumpInstructionBase jumpInst;
        UdonInstruction startInst;
        
        public WhileBlock(Node current, AssemblerState state, VariableName explicitTarget = default)
            : base(current, state, explicitTarget) {}

        protected override bool ResolveBlockType(out Type type) {
            if (Convert.ToString(current) == "while" && current.Count == 2) {
                state.tagTypeMapping.TryGetValue(current[1], out type);
                return true;
            }
            type = null;
            return false;
        }

        protected override bool Process(Stack<ProcessingBlock> stack) {
            var child = i < current.Count ? current[i] : null;
            switch (i) {
                case 0:
                    if (child == null) throw new Exception();
                    startInst = state.builder.LastInstruction;
                    stack.Push(Create(child, state, result = GetTempVariable(child)));
                    i++;
                    return false;
                case 1:
                    if (child == null) throw new Exception();
                    jumpInst = state.builder.EmitJumpIfFalse(result, 0);
                    ReturnAllTempVariables();
                    stack.Push(Create(child, state, ExplicitTarget));
                    i++;
                    return false;
                default:
                    jumpInst.destination = state.builder.EmitJump(startInst);
                    ReturnAllTempVariables();
                    return true;
            }
        }
    }
}