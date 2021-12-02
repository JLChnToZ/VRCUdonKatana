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
        [SerializeField] bool autoReload;
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
                if (EditorGUI.EndChangeCheck() && textAsset != null && sourceText != textAsset.text &&
                    EditorUtility.DisplayDialog("Load Udon Katana Script", "Do you want to overwrite existing scripts now?", "Yes", "No")) {
                    sourceText = textAsset.text;
                    dirty = true;
                } else if (textAsset == null)
                    autoReload = false;
            }
            using (new EditorGUI.DisabledScope(autoReload || (textAsset != null ? sourceText != textAsset.text : string.IsNullOrEmpty(sourceText))))
                if (GUILayout.Button("Save", EditorStyles.miniButtonLeft)) {
                    SaveAsset();
                    dirty = true;
                }
            using (new EditorGUI.DisabledScope(autoReload || textAsset == null || sourceText == textAsset.text))
                if (GUILayout.Button("Restore", EditorStyles.miniButtonMid)) {
                    sourceText = textAsset.text;
                    dirty = true;
                }
            using (new EditorGUI.DisabledScope(textAsset == null || (!autoReload && sourceText != textAsset.text))) {
                EditorGUI.BeginChangeCheck();
                autoReload = GUILayout.Toggle(autoReload, "Auto", EditorStyles.miniButtonRight);
                if (EditorGUI.EndChangeCheck()) {
                    sourceText = autoReload ? "" : textAsset.text;
                    dirty = true;
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            showKatana = EditorGUILayout.Foldout(showKatana, "Udon Katana");
            if (GUILayout.Button("Compile", EditorStyles.miniButton)) {
                RefreshProgram();
                dirty = true;
            }
            EditorGUILayout.EndHorizontal();
            using (new EditorGUI.DisabledScope(autoReload || !allowEditing))
                if (showKatana) {
                    EditorGUILayout.LabelField("Udon Katana", EditorStyles.boldLabel);
                    if (autoReload)
                        EditorGUILayout.TextArea(textAsset.text);
                    else {
                        EditorGUI.BeginChangeCheck();
                        sourceText = EditorGUILayout.TextArea(sourceText);
                        if (EditorGUI.EndChangeCheck()) dirty = true;
                    }
                }
            showAssembly = EditorGUILayout.Foldout(showAssembly, "Compiled Udon Assembly");
            if (showAssembly) base.DrawAssemblyTextArea(false, ref dirty);
        }

        protected override void RefreshProgramImpl() {
            try {
                var builder = new UdonAssemblyBuilder();
                if (autoReload && textAsset != null) sourceText = textAsset.text;
                var node = Node.Deserialize(sourceText);
                ProcessingBlock.AssembleBody(node, builder, sourceText);
                udonAssembly = builder.Compile();
                program = builder.Assemble();
                assemblyError = null;
            } catch (Exception ex) {
                Debug.LogError(ex);
                assemblyError = ex.Message;
            } finally {
                if (autoReload) sourceText = "";
            }
        }

        private void SaveAsset() {
            var assetPath = textAsset == null ? string.Empty : AssetDatabase.GetAssetPath(textAsset);
            if (string.IsNullOrEmpty(assetPath)) {
                assetPath = EditorUtility.SaveFilePanelInProject("Save Udon Katana Script", $"{name} Script", "txt", "");
                if (!string.IsNullOrEmpty(assetPath)) {
                    File.WriteAllText(assetPath, sourceText);
                    AssetDatabase.Refresh();
                    textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                }
            } else if (EditorUtility.DisplayDialog("Save Udon Katana Script", $"Do you want to overwrite `{assetPath}`?", "Yes", "No")) {
                File.WriteAllText(assetPath, sourceText);
                EditorUtility.SetDirty(textAsset);
            }
        }
    }

    [CustomEditor(typeof(UdonKatanaAsset))]
    internal class UdonKatanaAssetEditor: UdonAssemblyProgramAssetEditor {}
}