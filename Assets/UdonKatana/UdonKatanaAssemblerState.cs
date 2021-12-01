using System;
using System.Collections.Generic;
using VRC.Udon.Graph;
using VRC.Udon.Editor;
using JLChnToZ.VRC.UdonLowLevel;
using JLChnToZ.Katana.Expressions;

namespace JLChnToZ.VRC.UdonKatana {
    public struct AssemblerState {
        public readonly UdonAssemblyBuilder builder;
        public readonly Dictionary<Node, UdonNodeDefinition> nodeMapping;
        public readonly Dictionary<Node, Type> tagTypeMapping;
        public readonly Dictionary<string, (VariableName returnPointer, UdonInstruction lastInstruction, List<JumpInstruction> landingPoints)> entryPoints;
        public readonly Dictionary<Node, ProcessingBlock> processingBlocks;
        private readonly Dictionary<VariableName, Type> rentVariables;
        private readonly Dictionary<Type, (Queue<VariableName> varNames, int counter)> availableTempVariables;

        public AssemblerState(UdonAssemblyBuilder builder) {
            this.builder = builder;
            nodeMapping = new Dictionary<Node, UdonNodeDefinition>();
            tagTypeMapping = new Dictionary<Node, Type>();
            entryPoints = new Dictionary<string, (VariableName, UdonInstruction, List<JumpInstruction>)>();
            rentVariables = new Dictionary<VariableName, Type>();
            availableTempVariables = new Dictionary<Type, (Queue<VariableName>, int)>();
            processingBlocks = new Dictionary<Node, ProcessingBlock>();
        }

        public VariableName RentVariable(Node forNode) =>
            tagTypeMapping.TryGetValue(forNode, out var type) ? RentVariable(type) : default;

        public VariableName RentVariable(Type type) {
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
            availableVar.varNames.Enqueue(varName);
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
                switch (methodName) {
                    case "ctor":
                        methodName = $"Create{typeName}";
                        aliases.Add($"Create{def.type.GetUdonTypeName()}");
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
                            if (methodName.StartsWith("get_"))
                                methodName = $"Get{typeName}{char.ToUpper(methodName[4])}{methodName.Substring(5)}";
                            else if (methodName.StartsWith("set_"))
                                methodName = $"Set{typeName}{char.ToUpper(methodName[4])}{methodName.Substring(5)}";
                            else if (!methodName.StartsWith("op_"))
                                methodName = $"{typeName}{char.ToUpper(methodName[0])}{methodName.Substring(1)}";
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

        [UnityEditor.MenuItem("Tools/Udon Katana/Copy Supported Methods")]
        static void CopyNames() {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in methodDefs) {
                sb.AppendLine($"- {kv.Key.Item1} ({kv.Key.Item2})");
                foreach (var overload in kv.Value) {
                    sb.Append("   - ");
                    bool first = true;
                    foreach (var parameter in overload.parameters) {
                        if (first) first = false;
                        else sb.Append(", ");
                        sb.Append($"[{parameter.parameterType}] {parameter.type.Name}");
                    }
                    sb.AppendLine();
                }
            }
            UnityEditor.EditorGUIUtility.systemCopyBuffer = sb.ToString();
        }
    }
}