using System;
using System.Collections.Generic;
using VRC.Udon.Graph;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    [ProcessingBlockPriority(Priority = 998)]
    internal class ExternBlock: CallableBlockBase {
        readonly List<object> parameters = new List<object>();

        public ExternBlock(Node current, AssemblerState state)
            : base(current, state) {}

        protected override bool ResolveBlockType(out Type type) {
            if (current.Count == 0 && !isQuotedNoArgNode) {
                type = null;
                return false;
            }
            var tagStr = Convert.ToString(current);
            type = null;
            if (AssemblerStateHelper.methodDefs.TryGetValue((tagStr, current.Count), out var overloads)) {
                bool matches = false;
                foreach (var method in overloads) {
                    matches = true;
                    for (int i = 0; i < current.Count; i++) {
                        var parameter = method.parameters[i];
                        if (!state.tagTypeMapping.TryGetValue(current[i], out var currentType)) {
                            matches = false;
                            break;
                        }
                        switch (parameter.parameterType) {
                            case UdonNodeParameter.ParameterType.IN:
                                matches = parameter.type.IsAssignableFrom(currentType);
                                break;
                            case UdonNodeParameter.ParameterType.OUT:
                                matches = currentType.IsAssignableFrom(parameter.type);
                                break;
                            default:
                                matches = parameter.type == currentType;
                                break;
                        }
                        if (!matches) break;
                    }
                    if (matches) {
                        state.nodeMapping[current] = method;
                        if (method.parameters.Count > current.Count) // Has return type
                            type = method.parameters[current.Count].type;
                        break;
                    }
                }
                if (matches) return true;
            }
            return false;
        }
        
        protected override bool Process(Stack<ProcessingBlock> stack) {
            if (i < current.Count) {
                result = GetTempVariable(current[i]);
                stack.Push(Create(current[i], state, result));
                parameters.Add(result);
                i++;
                return false;
            }
            if (state.tagTypeMapping.ContainsKey(current))
                parameters.Add(ExplicitTarget.IsValid ? ExplicitTarget : GetTempVariable(current));
            if (state.nodeMapping.TryGetValue(current, out var def))
                state.builder.EmitExtern(def.fullName, parameters.ToArray());
            ReturnAllTempVariables();
            return true;
        }
    }
}