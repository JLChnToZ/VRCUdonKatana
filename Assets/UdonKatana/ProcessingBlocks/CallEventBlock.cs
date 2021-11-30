using System;
using System.Collections.Generic;
using JLChnToZ.VRC.UdonLowLevel;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    [ProcessingBlockPriority(Priority = 996)]
    internal class CallEventBlock: CallableBlockBase {
        public CallEventBlock(Node current, AssemblerState state, VariableName explicitTarget = default)
            : base(current, state, explicitTarget) {}

        protected override bool ResolveBlockType(out Type type) {
            if (state.entryPoints.ContainsKey(Convert.ToString(contentNode))) {
                type = typeof(object);
                return true;
            }
            type = null;
            return false;
        }
        
        protected override bool Process(Stack<ProcessingBlock> stack) {
            var tag = Convert.ToString(contentNode);
            if (state.entryPoints.TryGetValue(tag, out var ep)) {
                state.builder.EmitCopyOffset(ep.returnPointer, (int)JumpInstructionBase.SIZE);
                if (ep.lastInstruction != null)
                    state.builder.EmitJump(ep.lastInstruction.offset + ep.lastInstruction.Size);
                else
                    ep.landingPoints.Add(state.builder.EmitJump(0));
                state.builder.EmitCopy(UdonAssemblyBuilder.ReturnAddress, ep.returnPointer);
                if (ExplicitTarget.IsValid) state.builder.EmitCopy(returnValue, ExplicitTarget);
            }
            ReturnAllTempVariables();
            return true;
        }
    }
}