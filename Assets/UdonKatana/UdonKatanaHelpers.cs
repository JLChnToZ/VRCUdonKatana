using System;
using System.Collections.Generic;
using VRC.Udon;
using VRC.Udon.Graph;
using VRC.Udon.Editor;
using JLChnToZ.VRC.UdonLowLevel;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    internal struct AssemblerState {
        public readonly UdonAssemblyBuilder builder;
        public readonly Dictionary<Node, UdonNodeDefinition> nodeMapping;
        public readonly Dictionary<Node, Type> tagTypeMapping;
        public readonly Dictionary<string, (VariableName returnPointer, UdonInstruction lastInstruction, List<JumpInstruction> landingPoints)> entryPoints;
        private readonly Dictionary<VariableName, Type> rentVariables;
        private readonly Dictionary<Type, (Queue<VariableName> varNames, int counter)> availableTempVariables;

        public AssemblerState(UdonAssemblyBuilder builder) {
            this.builder = builder;
            nodeMapping = new Dictionary<Node, UdonNodeDefinition>();
            tagTypeMapping = new Dictionary<Node, Type>();
            entryPoints = new Dictionary<string, (VariableName, UdonInstruction, List<JumpInstruction>)>();
            rentVariables = new Dictionary<VariableName, Type>();
            availableTempVariables = new Dictionary<Type, (Queue<VariableName>, int)>();
        }

        public void ResolveTypes(Node node) {
            var stack = new Stack<Node>();
            var queue = new Queue<Node>();
            queue.Enqueue(node);
            // Walk through the node tree from the deepest.
            while (queue.Count > 0) {
                var current = queue.Dequeue();
                stack.Push(current);
                switch (Convert.ToString(current)) {
                    case "var": DeclareBlock.Declare(builder, current); continue;
                    case "uasm": continue;
                    case "when":
                        EntryPointBlock.DefineEntryPoint(current, this, true);
                        break;
                }
                foreach (var child in current)
                    queue.Enqueue(child);
            }
            while (stack.Count > 0) {
                var current = stack.Pop();
                if (current.Count == 0) {
                    tagTypeMapping[current] = current.Tag?.GetType() ?? typeof(object);
                    continue;
                }
                if (current.Tag == null && current.Count > 0) {
                    var child = current[current.Count - 1];
                    if (current.Count == 1) TryMatchType(child);
                    if (tagTypeMapping.TryGetValue(child, out var type))
                        tagTypeMapping[current] = type;
                    continue;
                }
                var tagStr = Convert.ToString(current);
                if (TryMatchType(current) || current.Count == 0) continue;
                if (tagStr == "$" && builder.TryGetVariable(Convert.ToString(current[0]), out var varDef)) {
                    // Variable Getter / Setter
                    tagTypeMapping[current] = varDef.type;
                    continue;
                }
                if (tagStr.StartsWith("!") && AssemblerStateHelper.typeDefs.TryGetValue(tagStr.Substring(1, tagStr.Length - 1), out var targetType)) {
                    // Generic Type Caster
                    tagTypeMapping[current] = targetType;
                    continue;
                }
            }
        }

        private bool TryMatchType(Node current, string tagStr = null) {
            if (string.IsNullOrEmpty(tagStr)) tagStr = Convert.ToString(current);
            if (AssemblerStateHelper.methodDefs.TryGetValue((tagStr, current.Count), out var overloads)) {
                bool matches = false;
                foreach (var method in overloads) {
                    matches = true;
                    for (int i = 0; i < current.Count; i++) {
                        var parameter = method.parameters[i];
                        if (!tagTypeMapping.TryGetValue(current[i], out var currentType)) {
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
                        nodeMapping[current] = method;
                        if (method.parameters.Count > current.Count) // Has return type
                            tagTypeMapping[current] = method.parameters[current.Count].type;
                        break;
                    }
                }
                if (matches) return true;
            }
            return false;
        }

        public VariableName RentVariable(Node forNode) {
            if (!tagTypeMapping.TryGetValue(forNode, out var type)) return default;
            if (!availableTempVariables.TryGetValue(type, out var availableVar))
                availableTempVariables.Add(type, availableVar = (new Queue<VariableName>(), 0));
            if (availableVar.varNames.Count > 0) return availableVar.varNames.Dequeue();
            var baseName = $"__temp_{type.GetUdonTypeName()}";
            while (builder.TryGetVariable($"{baseName}_{availableVar.counter}", out _))
                availableVar.counter++;
            availableTempVariables[type] = availableVar;
            var result = new VariableName($"{baseName}_{availableVar.counter}");
            builder.DefineVariable(result, type);
            rentVariables[result] = type;
            return result;
        }

        public void ReturnVariable(VariableName varName) {
            if (!varName.IsValid || !rentVariables.TryGetValue(varName, out var type)) return;
            rentVariables.Remove(varName);
            if (!availableTempVariables.TryGetValue(type, out var availableVar))
                availableTempVariables.Add(type, availableVar = (new Queue<VariableName>(), 0));
            availableVar.Item1.Enqueue(varName);
        }
    }

    internal static class AssemblerStateHelper {
        public static readonly Dictionary<(string, int), List<UdonNodeDefinition>> methodDefs = new Dictionary<(string, int), List<UdonNodeDefinition>>();
        public static readonly Dictionary<string, Type> typeDefs = new Dictionary<string, Type>();

        static string FormatTypeName(Type type) => type.IsArray ? $"{type.GetElementType().Name}Array" : type.Name;

        static AssemblerStateHelper() {
            var splitter = new [] { "__" };
            methodDefs.Clear();
            var aliases = new List<string>();
            foreach (var def in UdonEditorManager.Instance.GetNodeDefinitions()) {
                if (def.fullName.StartsWith("Type_") && !def.fullName.EndsWith("Ref")) {
                    typeDefs[FormatTypeName(def.type)] = def.type;
                    typeDefs[def.fullName.Substring(5)] = def.type;
                    continue;
                }
                var parsedName = def.fullName.Split(splitter, StringSplitOptions.None);
                if (parsedName.Length < 2) continue;
                aliases.Clear();
                int argc = def.parameters.Count;
                bool hasReturnType = argc > 0 && def.parameters[argc - 1].parameterType == UdonNodeParameter.ParameterType.OUT;
                if (hasReturnType) argc--;
                string methodName = parsedName[1];
                var typeName = FormatTypeName(def.type);
                var typeName2 = def.type.GetUdonTypeName();
                switch (methodName) {
                    case "ctor":
                        methodName = $"Create{typeName}";
                        aliases.Add($"Create{typeName2}");
                        break;
                    case "op_Explicit":
                    case "op_Implicit":
                        methodName = $"!{FormatTypeName(def.parameters[argc].type)}";
                        aliases.Add($"!{def.parameters[argc].type.GetUdonTypeName()}");
                        break;
                    case "op_UnaryPlus":
                    case "op_Increment":
                    case "op_Addition": methodName = "+"; break;
                    case "op_UnaryMinus":
                    case "op_Decrement":
                    case "op_Subtraction": methodName = "-"; break;
                    case "op_Multiply":
                    case "op_Multiplication": methodName = "*"; break;
                    case "op_Division": methodName = "/"; break;
                    case "op_Remainder":
                    case "op_Modulus": methodName = "%"; break;
                    case "op_Equality": methodName = "=="; break;
                    case "op_Inequality": methodName = "!="; break;
                    case "op_GreaterThan": methodName = ">"; break;
                    case "op_GreaterThanOrEqual": methodName = ">="; break;
                    case "op_LessThan": methodName = "<"; break;
                    case "op_LessThanOrEqual": methodName = "<="; break;
                    case "op_LeftShift": methodName = "<<"; break;
                    case "op_RightShift": methodName = ">>"; break;
                    case "op_LogicalAnd":
                    case "op_ConditionalAnd": methodName = "&"; break;
                    case "op_LogicalOr":
                    case "op_ConditionalOr": methodName = "|"; break;
                    case "op_LogicalXor":
                    case "op_ConditionalXor": methodName = "^"; break;
                    case "op_UnaryNegation": methodName = "~"; break;
                    default:
                        if (argc < 1 || def.parameters[0].type != def.type) {
                            if (methodName.StartsWith("get_")) {
                                aliases.Add($"Get{typeName2}{char.ToUpper(methodName[4])}{methodName.Substring(5)}");
                                methodName = $"Get{typeName}{char.ToUpper(methodName[4])}{methodName.Substring(5)}";
                            } else if (methodName.StartsWith("set_")) {
                                aliases.Add($"Set{typeName2}{char.ToUpper(methodName[4])}{methodName.Substring(5)}");
                                methodName = $"Set{typeName}{char.ToUpper(methodName[4])}{methodName.Substring(5)}";
                            } else if (!methodName.StartsWith("op_")) {
                                aliases.Add($"{typeName2}{char.ToUpper(methodName[0])}{methodName.Substring(1)}");
                                methodName = $"{typeName}{char.ToUpper(methodName[0])}{methodName.Substring(1)}";
                            }
                        } else if (methodName.StartsWith("get_"))
                            methodName = $"Get{char.ToUpper(methodName[4])}{methodName.Substring(5)}";
                        else if (methodName.StartsWith("set_"))
                            methodName = $"Set{char.ToUpper(methodName[4])}{methodName.Substring(5)}";
                        break;
                }
                AddMethodDefinition(methodName, argc, def);
                foreach (var alias in aliases) AddMethodDefinition(alias, argc, def);
            }
            foreach (var nodes in methodDefs.Values)
                nodes.Sort(UdonNodeDefinitionCompararer.instance);
        }

        static void AddMethodDefinition(string alias, int argc, UdonNodeDefinition def) {
            if (!methodDefs.TryGetValue((alias, argc), out var list))
                methodDefs.Add((alias, argc), list = new List<UdonNodeDefinition>());
            // Ensure there is no overloads with same signature registered
            // If there any, only keeps the parent definition.
            int i = list.FindIndex(entry => UdonNodeDefinitionCompararer.instance.Equals(entry, def));
            if (i < 0)
                list.Add(def);
            else if (!list[i].type.IsAssignableFrom(def.type))
                list[i] = def;
        }
    }

    internal abstract class ProcessingBlock {
        protected static readonly VariableName returnValue = UdonBehaviour.ReturnVariableName;
        public readonly Node current;
        protected readonly AssemblerState state;
        protected readonly VariableName explicitTarget;
        protected int i;
        protected VariableName result;
        private List<VariableName> rentVariables = new List<VariableName>();

        protected static ProcessingBlock Create(Node node, AssemblerState state, VariableName explicitTarget = default) {
            bool hasNullTagWrapped = false;
            while (node.Tag == null) {
                hasNullTagWrapped = true;
                if (node.Count == 0)
                    return new LiteralBlock(node, state, explicitTarget);
                if (node.Count > 1)
                    return new NoTagBlock(node, state, explicitTarget);
                node = node[0];
            }
            var tag = Convert.ToString(node);
            switch (tag) {
                case "if": return new IfBlock(node, state, explicitTarget);
                case "while": return new WhileBlock(node, state, explicitTarget);
                case "$": return new GetBlock(node, state, explicitTarget);
                case "=": return new SetBlock(node, state, explicitTarget);
                case "when": return new EntryPointBlock(node, state, explicitTarget);
                case "var": return new DeclareBlock(node, state, explicitTarget);
                case "uasm": return new UdonAssemblyBlock(node, state, explicitTarget);
                default:
                    return node.Count > 0 || hasNullTagWrapped ?
                        new ExternBlock(node, state, explicitTarget) as ProcessingBlock :
                        new LiteralBlock(node, state, explicitTarget) as ProcessingBlock;
            }
        }

        public static void AssembleBody(Node root, UdonAssemblyBuilder builder) {
            var stack = new Stack<ProcessingBlock>();
            var state = new AssemblerState(builder);
            state.ResolveTypes(root);
            stack.Push(Create(root, state));
            while (stack.Count > 0)
                if (stack.Peek().Process(stack))
                    stack.Pop();
        }

        protected ProcessingBlock(
            Node current,
            AssemblerState state,
            VariableName explicitTarget = default
        ) {
            this.current = current;
            this.state = state;
            this.explicitTarget = explicitTarget;
        }

        protected abstract bool Process(Stack<ProcessingBlock> stack);

        protected void ReturnAllTempVariables() {
            rentVariables.ForEach(state.ReturnVariable);
            rentVariables.Clear();
        }

        protected VariableName GetTempVariable(Node forNode) {
            var result = state.RentVariable(forNode);
            rentVariables.Add(result);
            return result;
        }
    }

    internal class NoTagBlock: ProcessingBlock {
        
        public NoTagBlock(Node current, AssemblerState state, VariableName explicitTarget = default)
            : base(current, state, explicitTarget) {}

        protected override bool Process(Stack<ProcessingBlock> stack) {
            if (i >= current.Count) return true;
            stack.Push(Create(current[i], state, i == current.Count - 1 ? explicitTarget : default));
            i++;
            return false;
        }
    }

    internal class IfBlock: ProcessingBlock {
        JumpInstructionBase jumpInst, jumpInst2;
        
        public IfBlock(Node current, AssemblerState state, VariableName explicitTarget = default)
            : base(current, state, explicitTarget) {}

        protected override bool Process(Stack<ProcessingBlock> stack) {
            var child = i < current.Count ? current[i] : null;
            switch (i) {
                case 0:
                    if (child == null) throw new Exception();
                    stack.Push(Create(child, state, result = GetTempVariable(child)));
                    i++;
                    return false;
                case 1:
                    if (child == null) throw new Exception();
                    jumpInst = state.builder.EmitJumpIfFalse(result, 0);
                    ReturnAllTempVariables();
                    stack.Push(Create(child, state, explicitTarget));
                    i++;
                    return false;
                case 2:
                    if (child != null) {
                        jumpInst.destination = jumpInst2 = state.builder.EmitJump();
                        stack.Push(Create(child, state, explicitTarget));
                    } else
                        jumpInst.destination = state.builder.LastInstruction;
                    i++;
                    ReturnAllTempVariables();
                    return child == null;
                default:
                    jumpInst2.destination = state.builder.LastInstruction;
                    ReturnAllTempVariables();
                    return true;
            }
        }
    }

    internal class WhileBlock: ProcessingBlock {
        JumpInstructionBase jumpInst;
        UdonInstruction startInst;
        
        public WhileBlock(Node current, AssemblerState state, VariableName explicitTarget = default)
            : base(current, state, explicitTarget) {}

        protected override bool Process(Stack<ProcessingBlock> stack) {
            var child = i < current.Count ? current[i] : null;
            switch (i) {
                case 0:
                    if (child == null) throw new Exception();
                    startInst = state.builder.LastInstruction;
                    stack.Push(Create(child, state, result = GetTempVariable(child)));
                    i++;
                    return false;
                case 1:
                    if (child == null) throw new Exception();
                    jumpInst = state.builder.EmitJumpIfFalse(result, 0);
                    ReturnAllTempVariables();
                    stack.Push(Create(child, state, explicitTarget));
                    i++;
                    return false;
                default:
                    jumpInst.destination = state.builder.EmitJump(startInst);
                    ReturnAllTempVariables();
                    return true;
            }
        }
    }

    internal class LiteralBlock: ProcessingBlock {
        public LiteralBlock(Node current, AssemblerState state, VariableName explicitTarget = default)
            : base(current, state, explicitTarget) {}
        
        protected override bool Process(Stack<ProcessingBlock> stack) {
            if (i == 0) {
                if (explicitTarget.IsValid)
                    state.builder.EmitCopy(current.Tag, explicitTarget);
                else
                    state.builder.EmitNop();
                i++;
            }
            return true;
        }

    }

    internal class GetBlock: ProcessingBlock {
        public GetBlock(Node current, AssemblerState state, VariableName explicitTarget = default)
            : base(current, state, explicitTarget) {}
        
        protected override bool Process(Stack<ProcessingBlock> stack) {
            if (i == 0 && explicitTarget.IsValid)
                state.builder.EmitCopy(new VariableName(Convert.ToString(current[0])), explicitTarget);
            i++;
            return true;
        }
    }

    internal class SetBlock: ProcessingBlock {
        public SetBlock(Node current, AssemblerState state, VariableName explicitTarget = default)
            : base(current, state, explicitTarget) {
            result = new VariableName(Convert.ToString(current[0]));
        }
        
        protected override bool Process(Stack<ProcessingBlock> stack) {
            switch (i) {
                case 0:
                    stack.Push(Create(current[1], state, result));
                    i++;
                    return false;
                case 1:
                    if (explicitTarget.IsValid)
                        state.builder.EmitCopy(new VariableName(Convert.ToString(current[0])), explicitTarget);
                    ReturnAllTempVariables();
                    i++;
                    return true;
                default:
                    ReturnAllTempVariables();
                    return true;
            }
        }
    }

    internal class ExternBlock: ProcessingBlock {
        readonly List<object> parameters = new List<object>();

        public ExternBlock(Node current, AssemblerState state, VariableName explicitTarget = default)
            : base(current, state, explicitTarget) {}
        
        protected override bool Process(Stack<ProcessingBlock> stack) {
            if (i < current.Count) {
                result = GetTempVariable(current[i]);
                stack.Push(Create(current[i], state, result));
                parameters.Add(result);
                i++;
                return false;
            }
            if (state.tagTypeMapping.ContainsKey(current))
                parameters.Add(explicitTarget.IsValid ? explicitTarget : GetTempVariable(current));
            var tag = Convert.ToString(current);
            if (state.nodeMapping.TryGetValue(current, out var def))
                state.builder.EmitExtern(def.fullName, parameters.ToArray());
            else if (tag.StartsWith("!"))
                state.builder.EmitCopy(parameters[0], explicitTarget);
            else if (state.entryPoints.TryGetValue(tag, out var ep)) {
                state.builder.EmitCopyOffset(ep.returnPointer, (int)JumpInstructionBase.SIZE);
                if (ep.lastInstruction != null)
                    state.builder.EmitJump(ep.lastInstruction.offset + ep.lastInstruction.Size);
                else
                    ep.landingPoints.Add(state.builder.EmitJump(0));
                state.builder.EmitCopy(UdonAssemblyBuilder.ReturnAddress, ep.returnPointer);
                if (explicitTarget.IsValid) state.builder.EmitCopy(returnValue, explicitTarget);
            } else
                throw new ArgumentException($"No matching method for {tag}!");
            ReturnAllTempVariables();
            return true;
        }
    }

    internal class EntryPointBlock: ProcessingBlock {
        public EntryPointBlock(Node current, AssemblerState state, VariableName explicitTarget = default)
            : base(current, state, explicitTarget) {}

        protected override bool Process(Stack<ProcessingBlock> stack) {
            var eventName = Convert.ToString(current[0]);
            switch (i) {
                case 0:
                    var (varName, _, landingPoints) = DefineEntryPoint(current, state);
                    if (explicitTarget.IsValid) {
                        state.builder.EmitCopyOffset(varName, (int)JumpInstructionBase.SIZE);
                        landingPoints.Add(state.builder.EmitJump(0));
                        state.builder.EmitCopy(UdonAssemblyBuilder.ReturnAddress, varName);
                        if (explicitTarget.IsValid) state.builder.EmitCopy(returnValue, explicitTarget);
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

        public static (VariableName returnPointer, UdonInstruction lastInstruction, List<JumpInstruction> landingPoints) DefineEntryPoint(Node node, AssemblerState state, bool isPredefined = false) {
            var eventName = Convert.ToString(node[0]);
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

    internal class DeclareBlock: ProcessingBlock {
        public DeclareBlock(Node current, AssemblerState state, VariableName explicitTarget = default)
            : base(current, state, explicitTarget) {}

        protected override bool Process(Stack<ProcessingBlock> stack) {
            if (i == 0) {
                DeclareUnchecked(state.builder, current);
                i++;
            }
            if (explicitTarget.IsValid) state.builder.EmitCopy(new VariableName(Convert.ToString(current[0])), explicitTarget);
            return true;
        }

        public static void Declare(UdonAssemblyBuilder builder, Node declareNode) {
            if (Convert.ToString(declareNode) != "var")
                throw new ArgumentException("Invalid node");
            DeclareUnchecked(builder, declareNode);
        }

        private static void DeclareUnchecked(UdonAssemblyBuilder builder, Node declareNode) {
            Type type = null;
            VariableAttributes attr = VariableAttributes.None;
            for (int i = 1; i < declareNode.Count - 1; i++) {
                var tag = Convert.ToString(declareNode[i]);
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
            var defaultValue = declareNode.Count > 1 ? declareNode[declareNode.Count - 1].Tag : null;
            builder.DefineVariable(Convert.ToString(declareNode[0]), type, attr, defaultValue);
        }
    }

    internal class UdonAssemblyBlock: ProcessingBlock {
        public UdonAssemblyBlock(Node current, AssemblerState state, VariableName explicitTarget = default)
            : base(current, state, explicitTarget) {}
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