using System;
using System.Linq;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRC.Udon.Graph;
using VRC.Udon.Editor;
using JLChnToZ.VRC.UdonLowLevel;

namespace JLChnToZ.VRC.UdonKatana {
    internal class CheatSheet : EditorWindow {
        public static CheatSheet GetWindow() => GetWindow<CheatSheet>(true);

        [MenuItem("Tools/Udon Katana/Show Cheat Sheet")]
        static void ShowWindow() => GetWindow<CheatSheet>(true);

        [SerializeField] string searchString;
        static readonly List<CheatSheetEntry> entries = new List<CheatSheetEntry>();
        static bool isReady;
        CheatSheetEntry[] filteredEntries;
        bool isInvoking = false;
        Vector2 scroll;

        void OnEnable() {
            titleContent = new GUIContent("Udon Katana Cheat Sheet");
            InitImpl();
            if (!string.IsNullOrEmpty(searchString)) Search();
        }
        
        void OnGUI() {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUI.BeginChangeCheck();
            searchString = Helper.ToolbarSearchField(searchString);
            if (EditorGUI.EndChangeCheck()) Search();
            EditorGUILayout.EndHorizontal();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (isInvoking)
                EditorGUILayout.LabelField("Searching");
            if (filteredEntries != null && filteredEntries.Length > 0)
                foreach (var entry in filteredEntries) {
                    EditorGUILayout.LabelField($"{entry.title} - {entry.type}", EditorStyles.boldLabel);
                    EditorGUILayout.BeginHorizontal(GUILayout.Height(entry.height * EditorGUIUtility.singleLineHeight));
                    EditorGUILayout.SelectableLabel(entry.csharp, GUILayout.ExpandHeight(true));
                    EditorGUILayout.SelectableLabel(entry.katana, GUILayout.ExpandHeight(true));
                    EditorGUILayout.EndHorizontal();
                }
            else if (isInvoking)
                EditorGUILayout.LabelField("No results...");
            else
                EditorGUILayout.LabelField("No results, please try other keywords.");
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndScrollView();
        }

        private void Search() {
            if (isInvoking || !isReady) return;
            isInvoking = true;
            new Thread(SearchImpl).Start();
        }

        private static void InitImpl() {
            if (entries.Count == 0) {
                var defs = new List<UdonNodeDefinition>(UdonEditorManager.Instance.GetNodeDefinitions());
                EditorUtility.DisplayProgressBar("Fetching types", "Fetching types", 0);
                for (int i = 0; i < defs.Count; i++) {
                    if (CheatSheetEntry.Create(defs[i], out var result))
                        entries.Add(result);
                    EditorUtility.DisplayProgressBar("Fetching types", $"{i} of {defs.Count}", (float)i / defs.Count);
                }
                EditorUtility.ClearProgressBar();
                isReady = true;
            }
        }

        private void SearchImpl(object state) {
            string localSearchString;
            var filteredEntries = new List<CheatSheetEntry>();
            do {
                localSearchString = searchString;
                filteredEntries.Clear();
                this.filteredEntries = null;
                string trimmedSearchString = localSearchString.Trim();
                foreach(var entry in entries) {
                    if (entry.csharp.IndexOf(trimmedSearchString, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                        entry.katana.IndexOf(trimmedSearchString, StringComparison.InvariantCultureIgnoreCase) >= 0) {
                        filteredEntries.Add(entry);
                        this.filteredEntries = filteredEntries.ToArray();
                        if (filteredEntries.Count > 100) break;
                    }
                    if (localSearchString != searchString) break;
                }
            } while (localSearchString != searchString);
            isInvoking = false;
        }
    }

    internal struct CheatSheetEntry {
        static readonly Regex illegalChars = new Regex("\\W+", RegexOptions.Compiled);
        public string title;
        public string type;
        public string csharp;
        public string katana;
        public int height;

        private void CalcluateHeight() {
            height = Math.Max(Helper.CalcluateLines(csharp), Helper.CalcluateLines(katana));
        }

        public static bool Create(UdonNodeDefinition src, out CheatSheetEntry result) {
            if (src.fullName.StartsWith("Event_") && src.fullName != "Event_Custom") {
                result = CreateEventEntry(src);
                return true;
            }
            if (src.fullName.StartsWith("Type_") && !src.fullName.EndsWith("Ref")) {
                result = new CheatSheetEntry {
                    title = src.name,
                    type = "Type",
                    csharp = $"{src.type.ToCSharpTypeName()} value;",
                    katana = $"var(value, {TypeHelper.GetUdonTypeName(src.type)}, )",
                };
                result.CalcluateHeight();
                return true;
            }
            var parsedName = src.fullName.Split(new [] { "__" }, StringSplitOptions.None);
            if (parsedName.Length == 4) {
                string methodName = parsedName[1];
                switch (methodName) {
                    case "op_Explicit": result = CreateCastEntry(src, true); return true;
                    case "op_Implicit": result = CreateCastEntry(src, false); return true;
                    case "op_Increment": result = CreateOperatorEntry(src, "++"); return true;
                    case "op_UnaryPlus":
                    case "op_Addition": result = CreateOperatorEntry(src, "+"); return true;
                    case "op_UnaryMinus":
                    case "op_Subtraction": result = CreateOperatorEntry(src, "-"); return true;
                    case "op_Decrement": result = CreateOperatorEntry(src, "--"); return true;
                    case "op_Multiply":
                    case "op_Multiplication": result = CreateOperatorEntry(src, "*"); return true;
                    case "op_Division": result = CreateOperatorEntry(src, "/"); return true;
                    case "op_Remainder":
                    case "op_Modulus": result = CreateOperatorEntry(src, "%"); return true;
                    case "op_Equality": result = CreateOperatorEntry(src, "=="); return true;
                    case "op_Inequality": result = CreateOperatorEntry(src, "!="); return true;
                    case "op_GreaterThan": result = CreateOperatorEntry(src, ">"); return true;
                    case "op_GreaterThanOrEqual": result = CreateOperatorEntry(src, ">="); return true;
                    case "op_LessThan": result = CreateOperatorEntry(src, "<"); return true;
                    case "op_LessThanOrEqual": result = CreateOperatorEntry(src, "<="); return true;
                    case "op_LeftShift": result = CreateOperatorEntry(src, "<<"); return true;
                    case "op_RightShift": result = CreateOperatorEntry(src, ">>"); return true;
                    case "op_LogicalAnd":
                    case "op_ConditionalAnd": result = CreateOperatorEntry(src, "&"); return true;
                    case "op_LogicalOr":
                    case "op_ConditionalOr": result = CreateOperatorEntry(src, "|"); return true;
                    case "op_LogicalXor":
                    case "op_ConditionalXor": result = CreateOperatorEntry(src, "^"); return true;
                    case "op_UnaryNegation": result = CreateOperatorEntry(src, "~"); return true;
                    case "ctor": result = CreateCtorEntry(src); return true;
                    default:
                        if (methodName.StartsWith("get_")) {
                            result = InitDeclaration(src, out var csBuilder, out var katanaBuilder);
                            var returnName = GetParameterName(src, -1);
                            if (src.parameters.Count == 0) {
                                csBuilder.Append($"{returnName} = {src.type.ToCSharpTypeName()}.{methodName.Substring(4)};");
                                katanaBuilder.Append($"=({returnName}, (Get{src.type.GetUdonTypeName()}{char.ToUpper(methodName[4])}{methodName.Substring(5)})),");
                            } else {
                                csBuilder.Append($"{returnName} = {src.parameters[0].name}.{methodName.Substring(4)};");
                                katanaBuilder.Append($"=({returnName}, Get{src.type.GetUdonTypeName()}{char.ToUpper(methodName[4])}{methodName.Substring(5)}($({src.parameters[0].name}))),");
                            }
                            Finalize(ref result, csBuilder, katanaBuilder);
                            return true;
                        } else if (methodName.StartsWith("set_")) {
                            result = InitDeclaration(src, out var csBuilder, out var katanaBuilder);
                            if (src.parameters.Count == 1) {
                                csBuilder.Append($"{src.type.ToCSharpTypeName()}.{methodName.Substring(4)} = {src.parameters[0].name};");
                                katanaBuilder.Append($"Set{src.type.GetUdonTypeName()}{char.ToUpper(methodName[4])}{methodName.Substring(5)}($({src.parameters[0].name})),");
                            } else {
                                csBuilder.Append($"{src.parameters[0].name}.{methodName.Substring(4)} = {src.parameters[1].name};");
                                katanaBuilder.Append($"Set{char.ToUpper(methodName[4])}{methodName.Substring(5)}($({src.parameters[0].name}), $({src.parameters[1].name})),");
                            }
                            Finalize(ref result, csBuilder, katanaBuilder);
                            return true;
                        } else {
                            int parameterCount = parsedName[2].Split('_').Length;
                            bool hasReturnType = parsedName[3] != "SystemVoid";
                            if (hasReturnType) parameterCount++;
                            bool isStatic = parameterCount == src.parameters.Count;
                            result = CreateMethodEntry(
                                src, $"{(isStatic ? src.type.ToCSharpTypeName() : GetParameterName(src, 0))}.{methodName}",
                                methodName, isStatic, hasReturnType
                            );
                            return true;
                        }
                }
            }
            result = default;
            return false;
        }

        private static CheatSheetEntry InitDeclaration(UdonNodeDefinition src, out StringBuilder csBuilder, out StringBuilder katanaBuilder) {
            csBuilder = new StringBuilder();
            katanaBuilder = new StringBuilder();
            var result = new CheatSheetEntry {
                title = src.name,
                type = "Method",
            };
            for (int i = 0; i < src.parameters.Count; i++) {
                var parameter = src.parameters[i];
                csBuilder.Append($"{parameter.type.ToCSharpTypeName()} {GetParameterName(src, i)};\n");
                katanaBuilder.Append($"var({GetParameterName(src, i)}, {parameter.type.GetUdonTypeName()}, ),\n");
            }
            csBuilder.Append("\n");
            katanaBuilder.Append("\n");
            return result;
        }

        private static string GetParameterName(UdonNodeDefinition src, int index) {
            if (index < 0) index += src.parameters.Count;
            var p = src.parameters[index];
            string name = p.name;
            return string.IsNullOrEmpty(name) ? p.parameterType == UdonNodeParameter.ParameterType.OUT ? "result" : $"arg{index}" : illegalChars.Replace(name, "");
        }

        private static CheatSheetEntry CreateEventEntry(UdonNodeDefinition src) {
            var returnType = typeof(void);
            foreach (var parameter in src.parameters)
                if (parameter.parameterType == UdonNodeParameter.ParameterType.IN) 
                    returnType = parameter.type;
            var csBuilder = new StringBuilder();
            var katanaBuilder = new StringBuilder();
            var methodName = src.fullName.Substring(6);
            csBuilder.Append($"{returnType.ToCSharpTypeName()} {methodName} (");
            katanaBuilder.Append($"when(_{char.ToLower(methodName[0])}{methodName.Substring(1)}, (\n");
            bool isFirst = true;
            for (int i = 0; i < src.parameters.Count; i++) {
                var parameter = src.parameters[i];
                var parameterName = GetParameterName(src, i);
                if (parameter.parameterType == UdonNodeParameter.ParameterType.IN) {
                    katanaBuilder.Append($"  =({char.ToLower(methodName[0])}{methodName.Substring(1)}{char.ToUpper(parameterName[0])}{parameterName.Substring(1)}, $({parameterName})),\n");
                    continue;
                }
                if (isFirst) isFirst = false;
                else {
                    csBuilder.Append(", ");
                    katanaBuilder.Append(", ");
                }
                if (parameter.parameterType == UdonNodeParameter.ParameterType.IN_OUT)
                    csBuilder.Append("ref ");
                csBuilder.Append($"{parameter.type.ToCSharpTypeName()} {parameterName}");
                katanaBuilder.Append($"  $({char.ToLower(methodName[0])}{methodName.Substring(1)}{char.ToUpper(parameterName[0])}{parameterName.Substring(1)}), ; {parameter.type.Name} {parameterName}\n");
            }
            csBuilder.Append(")\n{\n    // ...\n}");
            katanaBuilder.Append("\n))");
            var result = new CheatSheetEntry {
                title = src.name,
                type = "Event",
            };
            Finalize(ref result, csBuilder, katanaBuilder);
            return result;
        }

        private static CheatSheetEntry CreateCastEntry(UdonNodeDefinition src, bool isExplicit) =>
            CreateOperatorEntry(src, isExplicit ? $"({src.parameters[1].type.ToCSharpTypeName()})" : "", $"!{src.parameters[1].type.GetUdonTypeName()}");

        private static CheatSheetEntry CreateOperatorEntry(UdonNodeDefinition src, string csOp, string katanaOp = null) {
            if (string.IsNullOrEmpty(katanaOp)) katanaOp = csOp;
            var result = InitDeclaration(src, out var csBuilder, out var katanaBuilder);
            switch (src.parameters.Count) {
                case 2:
                    csBuilder.Append($"{GetParameterName(src, 1)} = {csOp}{GetParameterName(src, 0)};");
                    katanaBuilder.Append($"=({GetParameterName(src, 1)}, {katanaOp}($({GetParameterName(src, 0)}))),");
                    break;
                case 3:
                    csBuilder.Append($"{GetParameterName(src, 2)} = {GetParameterName(src, 0)} {csOp} {GetParameterName(src, 1)};");
                    katanaBuilder.Append($"=({GetParameterName(src, 2)}, {katanaOp}($({GetParameterName(src, 0)}), $({GetParameterName(src, 1)}))),");
                    break;
            }
            Finalize(ref result, csBuilder, katanaBuilder);
            return result;
        }

        private static CheatSheetEntry CreateCtorEntry(UdonNodeDefinition src) => CreateMethodEntry(src, $"new {src.type.ToCSharpTypeName()}", $"Create{src.type.GetUdonTypeName()}");

        private static CheatSheetEntry CreateMethodEntry(UdonNodeDefinition src, string csharpMethodName, string katanaMethodName, bool isStatic = true, bool hasReturnType = true) {
            var result = InitDeclaration(src, out var csBuilder, out var katanaBuilder);
            if (hasReturnType) {
                var returnName = GetParameterName(src, -1);
                csBuilder.Append($"{returnName} = {csharpMethodName}(");
                katanaBuilder.Append($"=({returnName}, {katanaMethodName}(");
            } else {
                csBuilder.Append($"{csharpMethodName}(");
                katanaBuilder.Append($"{katanaMethodName}(");
            }
            bool isCSFirst = true;
            bool isKatanaFirst = true;
            for (int i = 0; i < src.parameters.Count; i++) {
                var parameter = src.parameters[i];
                if (parameter.parameterType == UdonNodeParameter.ParameterType.OUT)
                    continue;
                var paramName = GetParameterName(src, i);
                if (isStatic || i > 0) {
                    if (isCSFirst) isCSFirst = false;
                    else csBuilder.Append(", ");
                    if (parameter.parameterType == UdonNodeParameter.ParameterType.IN_OUT)
                        csBuilder.Append("ref ");
                    csBuilder.Append(paramName);
                }
                if (isKatanaFirst) isKatanaFirst = false;
                else katanaBuilder.Append(", ");
                katanaBuilder.Append($"$({paramName})");
            }
            csBuilder.Append(");");
            katanaBuilder.Append(hasReturnType ? "))," : "),");
            Finalize(ref result, csBuilder, katanaBuilder);
            return result;
        }

        private static void Finalize(ref CheatSheetEntry result, StringBuilder csBuilder, StringBuilder katanaBuilder) {
            result.csharp = csBuilder.ToString();
            result.katana = katanaBuilder.ToString();
            result.CalcluateHeight();
        }
    }
}