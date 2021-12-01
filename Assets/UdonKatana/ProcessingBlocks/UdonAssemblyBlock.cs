using System;
using System.Collections.Generic;
using JLChnToZ.VRC.UdonLowLevel;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    [ProcessingBlockPriority(Priority = 0)]
    internal class UdonAssemblyBlock: ProcessingBlock {
        public UdonAssemblyBlock(Node current, AssemblerState state)
            : base(current, state) {}

        protected override bool BeforeResolveBlockType() => Convert.ToString(current) != "uasm";

        protected override bool ResolveBlockType(out Type type) {
            type = null;
            return Convert.ToString(current) == "uasm";
        }

        protected override bool Process(Stack<ProcessingBlock> stack) {
            if (i == 0) {
                foreach (var node in current) {
                    var tag = Convert.ToString(node);
                    switch (tag.ToLower()) {
                        case "nop": state.builder.EmitNop(); break;
                        case "push": state.builder.EmitPush(new VariableName(Convert.ToString(node[0]))); break;
                        case "pop": state.builder.EmitPop(); break;
                        case "jumpiffalse": state.builder.EmitJumpIfFalse(Convert.ToString(node[0])); break;
                        case "jump": state.builder.EmitJump(Convert.ToUInt32(node[0])); break;
                        case "extern": state.builder.EmitExtern(Convert.ToString(node[0])); break;
                        case "annontation": break;
                        case "jumpindirect": state.builder.EmitJumpIndirect(Convert.ToString(node[0])); break;
                        case "copy": state.builder.EmitCopy(); break;
                        default: throw new ArgumentException($"Unknown UAssembly insturction `{tag}`.");
                    }
                }
                i++;
            }
            return true;
        }
    }
}