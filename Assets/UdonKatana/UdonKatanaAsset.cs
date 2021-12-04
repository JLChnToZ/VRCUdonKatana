using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using VRC.Udon;
using VRC.Udon.Editor.ProgramSources;
using VRC.Udon.Editor.ProgramSources.Attributes;
using JLChnToZ.VRC.UdonLowLevel;
using JLChnToZ.VRC.UdonKatana;
using JLChnToZ.Katana.Expressions;

[assembly: UdonProgramSourceNewMenu(typeof(UdonKatanaAsset), "Udon Katana Asset")]
namespace JLChnToZ.VRC.UdonKatana {
    [CreateAssetMenu(menuName = "VRChat/Udon/Udon Katana Asset")]
    public class UdonKatanaAsset: UdonAssemblyProgramAsset {
        private const string defaultSource = "(\n  ; Type in your Udon Katana source code here.\n\n  when(_start, (\n    ; Called on initialize\n    DebugLog(Udon Katana works!)\n  )),\n\n  when(_update, (\n    ; Called on every frame\n  )),\n)";
        [SerializeField] string sourceText;
        [SerializeField] TextAsset textAsset;
        [SerializeField] bool showKatana;
        [SerializeField] bool showAssembly;
        [SerializeField] bool showDasm;

        void Reset() {
            sourceText = defaultSource;
            showKatana = true;
            showAssembly = false;
            showDasm = false;
        }

        protected override void DrawProgramSourceGUI(UdonBehaviour udonBehaviour, ref bool dirty) {
            DrawAssemblyTextArea(!Application.isPlaying, ref dirty);
            DrawAssemblyErrorTextArea();
            DrawPublicVariables(udonBehaviour, ref dirty);
            showDasm = EditorGUILayout.Foldout(showDasm, "Disassembled Program");
            if (showDasm) DrawProgramDisassembly();
        }

        protected override void DrawAssemblyTextArea(bool allowEditing, ref bool dirty) {
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!allowEditing)) {
                EditorGUI.BeginChangeCheck();
                textAsset = EditorGUILayout.ObjectField("Script File (Optional)", textAsset, typeof(TextAsset), false) as TextAsset;
                if (EditorGUI.EndChangeCheck() && textAsset != null) {
                    if (!string.IsNullOrEmpty(sourceText) && sourceText != textAsset.text)
                        switch (EditorUtility.DisplayDialogComplex(
                            "Load Udon Katana Script",
                            "You have existing scripts, do you want to overwrite it?",
                            "Use the script file",
                            "Overwrite the script file with existing script",
                            "Cancel"
                        )) {
                            case 0:
                                sourceText = textAsset.text;
                                break;
                            case 1:
                                if (!SaveTextAsset()) goto default;
                                sourceText = "";
                                break;
                            default:
                                textAsset = null;
                                break;
                        }
                    else sourceText = "";
                    dirty = true;
                }
            }
            if (textAsset == null && GUILayout.Button("Save", EditorStyles.miniButton, GUILayout.ExpandWidth(false))) {
                SaveTextAsset();
                dirty = true;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            showKatana = EditorGUILayout.Foldout(showKatana, "Udon Katana");
            if (GUILayout.Button("Compile", EditorStyles.miniButtonLeft, GUILayout.ExpandWidth(false))) {
                RefreshProgram();
                dirty = true;
            }
            if (GUILayout.Button("Cheat Sheet...", EditorStyles.miniButtonRight, GUILayout.ExpandWidth(false)))
                CheatSheet.GetWindow();
            EditorGUILayout.EndHorizontal();
            using (new EditorGUI.DisabledScope(textAsset != null || !allowEditing))
                if (showKatana) {
                    EditorGUILayout.LabelField("Udon Katana", EditorStyles.boldLabel);
                    if (textAsset == null) {
                        EditorGUI.BeginChangeCheck();
                        sourceText = EditorGUILayout.TextArea(sourceText);
                        if (EditorGUI.EndChangeCheck()) dirty = true;
                    } else EditorGUILayout.TextArea(textAsset.text);
                }
            showAssembly = EditorGUILayout.Foldout(showAssembly, "Compiled Udon Assembly");
            if (showAssembly) base.DrawAssemblyTextArea(false, ref dirty);
        }

        protected override void RefreshProgramImpl() {
            try {
                var builder = new UdonAssemblyBuilder();
                if (textAsset != null) sourceText = textAsset.text;
                var node = Node.Deserialize(sourceText);
                ProcessingBlock.AssembleBody(node, builder, sourceText);
                udonAssembly = builder.Compile();
                program = builder.Assemble();
                assemblyError = null;
            } catch (Exception ex) {
                Debug.LogError(ex);
                assemblyError = ex.Message;
            } finally {
                if (textAsset != null) sourceText = "";
            }
        }

        private bool SaveTextAsset() {
            var assetPath = textAsset == null ? string.Empty : AssetDatabase.GetAssetPath(textAsset);
            if (string.IsNullOrEmpty(assetPath)) {
                assetPath = EditorUtility.SaveFilePanelInProject("Save Udon Katana Script", $"{name} Script", "txt", "");
                if (string.IsNullOrEmpty(assetPath)) return false;
                File.WriteAllText(assetPath, sourceText);
                AssetDatabase.Refresh();
                textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                return true;
            }
            File.WriteAllText(assetPath, sourceText);
            EditorUtility.SetDirty(textAsset);
            return true;
        }
    }

    [CustomEditor(typeof(UdonKatanaAsset))]
    internal class UdonKatanaAssetEditor: UdonAssemblyProgramAssetEditor {}
}