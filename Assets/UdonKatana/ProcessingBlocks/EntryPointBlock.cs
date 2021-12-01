using System;
using System.Collections.Generic;
using JLChnToZ.VRC.UdonLowLevel;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    [ProcessingBlockPriority(Priority = 0)]
    internal class EntryPointBlock: ProcessingBlock {
        public EntryPointBlock(Node current, AssemblerState state)
            : base(current, state) {}
        protected override bool BeforeResolveBlockType() {
            if (Convert.ToString(current) == "when") DefineEntryPoint(true);
            return true;
        }

        protected override bool ResolveBlockType(out Type type) {
            if (Convert.ToString(current) == "when" && current.Count == 2) {
                state.tagTypeMapping.TryGetValue(current[1], out type);
                return true;
            }
            type = null;
            return false;
        }

        protected override bool Process(Stack<ProcessingBlock> stack) {
            var eventName = Convert.ToString(current[0]);
            switch (i) {
                case 0:
                    var (varName, _, landingPoints) = DefineEntryPoint();
                    if (ExplicitTarget.IsValid) {
                        state.builder.EmitCopyOffset(varName, (int)JumpInstructionBase.SIZE);
                        landingPoints.Add(state.builder.EmitJump(0));
                        state.builder.EmitCopy(UdonAssemblyBuilder.ReturnAddress, varName);
                        if (ExplicitTarget.IsValid) state.builder.EmitCopy(returnValue, ExplicitTarget);
                    }
                    state.builder.DefineEvent(eventName);
                    stack.Push(Create(current[1], state, returnValue));
                    i++;
                    return false;
                case 1:
                    if (state.entryPoints.TryGetValue(eventName, out var ep)) {
                        state.builder.EmitJumpIndirect(ep.returnPointer);
                        foreach (var landingPoint in ep.landingPoints)
                            landingPoint.destination = ep.lastInstruction.next;
                        ep.landingPoints.Clear();
                    }
                    i++;
                    return true;
                default: return true;
            }
        }

        private (VariableName returnPointer, UdonInstruction lastInstruction, List<JumpInstruction> landingPoints) DefineEntryPoint(bool isPredefined = false) {
            var eventName = Convert.ToString(current[0]);
            if (!state.entryPoints.TryGetValue(eventName, out var ev)) {
                var baseVarName = $"__{eventName}_return";
                VariableName varName = baseVarName;
                int j = 0;
                while (state.builder.TryGetVariable(varName, out _))
                    varName = $"{baseVarName}_{j++}";
                state.builder.DefineVariable(varName, value: UdonAssemblyBuilder.ReturnAddress);
                ev = (varName, isPredefined ? null : state.builder.LastInstruction, new List<JumpInstruction>());
            } else if (!isPredefined)
                ev.lastInstruction = state.builder.LastInstruction;
            state.entryPoints[eventName] = ev;
            return ev;
        }
    }
}