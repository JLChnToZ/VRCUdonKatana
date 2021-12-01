using System;
using System.Collections.Generic;
using JLChnToZ.VRC.UdonLowLevel;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    [ProcessingBlockPriority(Priority = 990)]
    internal class ArrayFillBlock: ProcessingBlock {
        const string SetValue = "SystemArray.__SetValue__SystemObject_SystemInt32__SystemVoid";
        const string CreateArray = "SystemArray.__CreateInstance__SystemType_SystemInt32__SystemArray";

        VariableName tempValue;
        public ArrayFillBlock(Node current, AssemblerState state)
            : base(current, state) {}

        protected override bool ResolveBlockType(out Type type) {
            var tag = Convert.ToString(current.Tag);
            if (tag.StartsWith("To") && tag.EndsWith("Array") &&
                AssemblerStateHelper.typeDefs.TryGetValue(tag.Substring(2, tag.Length - 2), out type))
                return true;
            type = null;
            return false;
        }

        protected override bool Process(Stack<ProcessingBlock> stack) {
            if (i > 0) state.builder.EmitExtern(SetValue, result, tempValue, i - 1);
            if (i >= current.Count) {
                ReturnAllTempVariables();
                return true;
            }
            if (i == 0 && state.tagTypeMapping.TryGetValue(current, out var type)) {
                result = ExplicitTarget.IsValid ? ExplicitTarget : GetTempVariable(type);
                tempValue = GetTempVariable(type.GetElementType());
                state.builder.EmitExtern(CreateArray, type, current.Count, result);
            }
            stack.Push(Create(current[i], state, tempValue));
            i++;
            return false;
        }
    }
}
