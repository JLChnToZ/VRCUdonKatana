using System;
using System.Collections.Generic;
using JLChnToZ.VRC.UdonLowLevel;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    [ProcessingBlockPriority(Priority = 0)]
    internal class IfBlock: ProcessingBlock {
        JumpInstructionBase jumpInst;
        readonly List<JumpInstructionBase> jumpToEnd = new List<JumpInstructionBase>();
        
        public IfBlock(Node current, AssemblerState state)
            : base(current, state) {}

        protected override bool ResolveBlockType(out Type type) {
            if (Convert.ToString(current) == "if" && current.Count > 1) {
                var testTypes = new List<Type>();
                for (int i = 1; i < current.Count; i += 2) {
                    if (state.tagTypeMapping.TryGetValue(current[i], out Type childType))
                        testTypes.Add(childType);
                    else {
                        testTypes.Clear();
                        break;
                    }
                }
                if (current.Count % 2 == 1) {
                    if (state.tagTypeMapping.TryGetValue(current[current.Count - 1], out Type childType))
                        testTypes.Add(childType);
                    else
                        testTypes.Clear();
                }
                if (testTypes.Count > 0) {
                    type = testTypes[0];
                    bool satisfied = false;
                    while (!satisfied && type != null) {
                        satisfied = true;
                        for (int i = 1; i < testTypes.Count; i++)
                            if (!TypeHelper.IsTypeAssignable(type, testTypes[i])) {
                                satisfied = false;
                                break;
                            }
                        if (!satisfied)
                            type = type.BaseType;
                    }
                } else
                    type = null;
                return true;
            }
            type = null;
            return false;
        }

        protected override bool Process(Stack<ProcessingBlock> stack) {
            if (i >= current.Count) {
                var dest = state.builder.LastInstruction;
                foreach (var jump2 in jumpToEnd)
                    jump2.destination = dest;
                if (jumpInst != null) jumpInst.destination = dest;
                jumpToEnd.Clear();
                ReturnAllTempVariables();
                return true;
            }
            var child = current[i];
            switch (i % 2) {
                case 0:
                    if (i > 0) {
                        var jump2 = state.builder.EmitJump();
                        jumpToEnd.Add(jump2);
                        if (jumpInst != null) {
                            jumpInst.destination = jump2;
                            jumpInst = null;
                        }
                    }
                    if (i < current.Count - 1)
                        stack.Push(Create(child, state, result = GetTempVariable(child)));
                    else
                        stack.Push(Create(child, state, ExplicitTarget));
                    break;
                case 1:
                    jumpInst = state.builder.EmitJumpIfFalse(result, 0);
                    ReturnAllTempVariables();
                    stack.Push(Create(child, state, ExplicitTarget));
                    break;
            }
            i++;
            return false;
        }
    }
}