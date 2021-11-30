using System;
using System.Collections.Generic;
using JLChnToZ.VRC.UdonLowLevel;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    [ProcessingBlockPriority(Priority = 0)]
    internal class DeclareBlock: ProcessingBlock {
        public DeclareBlock(Node current, AssemblerState state, VariableName explicitTarget = default)
            : base(current, state, explicitTarget) {}

        protected override bool BeforeResolveBlockType() {
            if (Convert.ToString(current) == "var") Declare();
            return true;
        }

        protected override bool ResolveBlockType(out Type type) {
            if (Convert.ToString(current) == "var" && current.Count > 1) {
                AssemblerStateHelper.typeDefs.TryGetValue(Convert.ToString(current[0]), out type);
                return true;
            }
            type = null;
            return Convert.ToString(current) == "var" && current.Count > 1;
        }

        protected override bool Process(Stack<ProcessingBlock> stack) {
            if (i == 0) {
                Declare();
                i++;
            }
            if (ExplicitTarget.IsValid) state.builder.EmitCopy(new VariableName(Convert.ToString(current[0])), ExplicitTarget);
            return true;
        }

        private void Declare() {
            Type type = null;
            VariableAttributes attr = VariableAttributes.None;
            for (int i = 1; i < current.Count - 1; i++) {
                var tag = Convert.ToString(current[i]);
                switch (tag.ToLower()) {
                    case "public": attr |= VariableAttributes.Public; break;
                    case "private": attr &= ~VariableAttributes.Public; break;
                    case "sync": attr |= VariableAttributes.SyncNone; break;
                    case "linearsync": attr |= VariableAttributes.SyncLinear; break;
                    case "smoothsync": attr |= VariableAttributes.SyncSmooth; break;
                    case "this": attr |= VariableAttributes.DefaultThis; break;
                    default:
                        if (AssemblerStateHelper.typeDefs.TryGetValue(tag, out var gotType))
                            type = gotType;
                        break;
                }
            }
            var defaultValue = current.Count > 1 ? current[current.Count - 1].Tag : null;
            state.builder.DefineVariable(Convert.ToString(current[0]), type, attr, defaultValue);
        }
    }
}