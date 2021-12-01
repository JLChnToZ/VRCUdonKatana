using System;
using System.Collections.Generic;
using JLChnToZ.VRC.UdonLowLevel;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    [ProcessingBlockPriority(Priority = 0)]
    internal class DeclareBlock: ProcessingBlock {
        public DeclareBlock(Node current, AssemblerState state)
            : base(current, state) {}

        protected override bool BeforeResolveBlockType() {
            if (Convert.ToString(current) == "var" && current.Count > 0) {
                Declare();
                return false;
            }
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
                    default:
                        if (AssemblerStateHelper.typeDefs.TryGetValue(tag, out var gotType))
                            type = gotType;
                        break;
                }
            }
            var valueNode = current.Count > 1 ? current[current.Count - 1] : null;
            object defaultValue = null;
            if (valueNode == null) {}
            else if (valueNode.Count == 1 && valueNode.Tag == null)
                switch (Convert.ToString(valueNode[0]).ToLower()) {
                    case "this": attr |= VariableAttributes.DefaultThis; break;
                    case "create": defaultValue = ParseDefaultValue(type, valueNode[0]); break;
                }
            else if (valueNode.Count > 1)
                switch (Convert.ToString(valueNode).ToLower()) {
                    case "create": defaultValue = ParseDefaultValue(type, valueNode); break;
                }
            else
                defaultValue = valueNode.Tag;
            state.builder.DefineVariable(Convert.ToString(current[0]), type, attr, defaultValue);
        }

        private static object ParseDefaultValue(Type type, IList<Node> valueNode) {
            var parameters = new object[valueNode.Count];
            for (int i = 0; i < parameters.Length; i++)
                parameters[i] = valueNode[i].Tag;
            return Activator.CreateInstance(type ?? typeof(object), parameters);
        }
    }
}