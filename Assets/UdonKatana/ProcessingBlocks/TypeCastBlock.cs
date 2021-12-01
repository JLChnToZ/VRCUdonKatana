using System;
using System.Collections.Generic;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    [ProcessingBlockPriority(Priority = 997)]
    internal class TypeCastBlock: CallableBlockBase {
        public TypeCastBlock(Node current, AssemblerState state)
            : base(current, state) {}

        protected override bool ResolveBlockType(out Type type) {
            if (current.Count == 0 && !isQuotedNoArgNode) {
                type = null;
                return false;
            }
            var tagStr = Convert.ToString(current);
            if (tagStr.StartsWith("!") && AssemblerStateHelper.typeDefs.TryGetValue(tagStr.Substring(1), out type))
                return true;
            type = null;
            return false;
        }
        
        protected override bool Process(Stack<ProcessingBlock> stack) {
            if (i < current.Count) {
                result = GetTempVariable(current[i]);
                stack.Push(Create(current[i], state, result));
                i++;
                return false;
            }
            if (ExplicitTarget.IsValid) {
                if (result.IsValid)
                    state.builder.EmitCopy(result, ExplicitTarget);
                else
                    state.builder.EmitCopy(null, ExplicitTarget);
            }
            ReturnAllTempVariables();
            return true;
        }
    }
}