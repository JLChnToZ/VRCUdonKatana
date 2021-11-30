using System;
using System.Collections.Generic;
using VRC.Udon.Graph;
using JLChnToZ.VRC.UdonLowLevel;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    [ProcessingBlockPriority(Priority = 998)]
    internal class ExternBlock: CallableBlockBase {
        readonly List<object> parameters = new List<object>();

        public ExternBlock(Node current, AssemblerState state, VariableName explicitTarget = default)
            : base(current, state, explicitTarget) {}

        protected override bool ResolveBlockType(out Type type) {
            var tagStr = Convert.ToString(contentNode);
            type = null;
            if (AssemblerStateHelper.methodDefs.TryGetValue((tagStr, contentNode.Count), out var overloads)) {
                bool matches = false;
                foreach (var method in overloads) {
                    matches = true;
                    for (int i = 0; i < contentNode.Count; i++) {
                        var parameter = method.parameters[i];
                        if (!state.tagTypeMapping.TryGetValue(contentNode[i], out var currentType)) {
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
                        if (method.parameters.Count > contentNode.Count) // Has return type
                            type = method.parameters[contentNode.Count].type;
                        break;
                    }
                }
                if (matches) return true;
            }
            return false;
        }
        
        protected override bool Process(Stack<ProcessingBlock> stack) {
            if (i < contentNode.Count) {
                result = GetTempVariable(contentNode[i]);
                stack.Push(Create(contentNode[i], state, result));
                parameters.Add(result);
                i++;
                return false;
            }
            if (state.tagTypeMapping.ContainsKey(contentNode))
                parameters.Add(ExplicitTarget.IsValid ? ExplicitTarget : GetTempVariable(contentNode));
            if (state.nodeMapping.TryGetValue(current, out var def))
                state.builder.EmitExtern(def.fullName, parameters.ToArray());
            ReturnAllTempVariables();
            return true;
        }
    }
}