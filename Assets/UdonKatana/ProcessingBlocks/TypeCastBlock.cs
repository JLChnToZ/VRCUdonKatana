using System;
using System.Collections.Generic;
using JLChnToZ.VRC.UdonLowLevel;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    [ProcessingBlockPriority(Priority = 997)]
    internal class TypeCastBlock: CallableBlockBase {
        public TypeCastBlock(Node current, AssemblerState state, VariableName explicitTarget = default)
            : base(current, state, explicitTarget) {}

        protected override bool ResolveBlockType(out Type type) {
            var tagStr = Convert.ToString(contentNode);
            if (tagStr.StartsWith("!") && AssemblerStateHelper.typeDefs.TryGetValue(tagStr.Substring(1), out type))
                return true;
            type = null;
            return false;
        }
        
        protected override bool Process(Stack<ProcessingBlock> stack) {
            if (i < contentNode.Count) {
                result = GetTempVariable(contentNode[i]);
                stack.Push(Create(contentNode[i], state, result));
                i++;
                return false;
            }
            state.builder.EmitCopy(result, ExplicitTarget);
            ReturnAllTempVariables();
            return true;
        }
    }
}