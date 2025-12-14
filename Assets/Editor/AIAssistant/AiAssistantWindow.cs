using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AIAssistant
{
    public class AiAssistantWindow : EditorWindow
    {
        // UI state
        private string _userInput = "";
        private string _lastLoadedJson = "";
        private Vector2 _logScroll;

        // Log buffer
        private readonly List<string> _log = new List<string>();

        // Mode toggle (defaults to DryRun)
        private ExecutionMode _mode = ExecutionMode.DryRun;

        [MenuItem("AI/AI Editor Assistant")]
        public static void Open()
        {
            var window = GetWindow<AiAssistantWindow>("AI Editor Assistant");
            window.minSize = new Vector2(640, 420);
        }

        private void OnEnable()
        {
            if (_log.Count == 0)
            {
                AppendLog("AI Editor Assistant ready.");
                AppendLog("Tip: Paste JSON and click DryRun. Load Command File does NOT auto-run.");
            }
        }

        private void OnGUI()
        {
            // IMPORTANT: Never early-return out of OnGUI after Begin/End calls.
            try
            {
                DrawHeader();

                GUILayout.Space(6);

                DrawInputArea();

                GUILayout.Space(6);

                DrawButtonsRow();

                GUILayout.Space(8);

                DrawLogArea();
            }
            catch (Exception ex)
            {
                // Catch-all so OnGUI never breaks layout state
                AppendLog("[EXCEPTION] " + ex);
                Debug.LogException(ex);
            }
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("AI Editor Assistant", EditorStyles.boldLabel);

                GUILayout.FlexibleSpace();

                // Mode toggle (toolbar)
                var modes = new[] { "DryRun", "Apply" };
                var idx = _mode == ExecutionMode.Apply ? 1 : 0;
                var nextIdx = GUILayout.Toolbar(idx, modes, GUILayout.Width(160));
                _mode = nextIdx == 1 ? ExecutionMode.Apply : ExecutionMode.DryRun;
            }
        }

        private void DrawInputArea()
        {
            EditorGUILayout.LabelField("Instruction / JSON", EditorStyles.miniBoldLabel);

            // Big text area for JSON
            _userInput = EditorGUILayout.TextArea(_userInput, GUILayout.MinHeight(140));
        }

        private void DrawButtonsRow()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                // DryRun uses explicit mode regardless of toolbar (convenience)
                if (GUILayout.Button("DryRun", GUILayout.Width(90)))
                    ExecuteFromUi(ExecutionMode.DryRun);

                if (GUILayout.Button("Apply", GUILayout.Width(90)))
                    ExecuteFromUi(ExecutionMode.Apply);

                GUILayout.Space(10);

                if (GUILayout.Button("Load Command File", GUILayout.Width(150)))
                    LoadCommandFile();

                if (GUILayout.Button("Clear Log", GUILayout.Width(110)))
                    _log.Clear();

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawLogArea()
        {
            EditorGUILayout.LabelField("Conversation / Log", EditorStyles.miniBoldLabel);

            using (var scroll = new EditorGUILayout.ScrollViewScope(_logScroll))
            {
                _logScroll = scroll.scrollPosition;

                if (_log.Count == 0)
                {
                    EditorGUILayout.HelpBox("No log output yet.", MessageType.Info);
                    return;
                }

                foreach (var line in _log)
                {
                    EditorGUILayout.SelectableLabel(line, GUILayout.Height(16));
                }
            }
        }

        private void LoadCommandFile()
        {
            var path = EditorUtility.OpenFilePanel("Select Command JSON File", Application.dataPath, "json");
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                var fileContent = File.ReadAllText(path);

                _lastLoadedJson = fileContent;
                _userInput = fileContent;

                AppendLog($"Loaded command file: {path} (not executed)");
                AppendLog("Raw JSON:");
                AppendLog(fileContent);
            }
            catch (Exception ex)
            {
                AppendLog("[ERROR] Failed to read file: " + ex.Message);
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// Single execution path for both DryRun and Apply to avoid duplicate variables / scope bugs.
        /// Never called from inside a GUILayout Begin/End without being wrapped in a scope.
        /// </summary>
        private void ExecuteFromUi(ExecutionMode mode)
        {
            AppendLog($"[UI] {mode} clicked");

            var json = !string.IsNullOrWhiteSpace(_userInput) ? _userInput : _lastLoadedJson;
            if (string.IsNullOrWhiteSpace(json))
            {
                AppendLog("[ERROR] No JSON in input box and no loaded command file.");
                return;
            }

            // Parse
            AiCommandEnvelope envelope = null;
            AiCommandList list = null;

            // Try envelope first (preferred)
            try { envelope = JsonUtility.FromJson<AiCommandEnvelope>(json); }
            catch { envelope = null; }

            if (envelope != null && envelope.commands != null && envelope.commands.commands != null && envelope.commands.commands.Length > 0)
            {
                list = envelope.commands;
            }
            else
            {
                // Fallback: legacy list-only JSON
                try { list = JsonUtility.FromJson<AiCommandList>(json); }
                catch { list = null; }
            }

            if (list == null || list.commands == null || list.commands.Length == 0)
            {
                AppendLog("[ERROR] Parsed no commands. Check JSON shape. Expected either:");
                AppendLog(" - { \"schemaVersion\":..., \"commands\": { \"commands\": [ ... ] } }");
                AppendLog(" - { \"commands\": [ ... ] }");
                return;
            }

            // Execute using reflection so UnityTools signature changes won't brick compilation.
            try
            {
                var resultObj = InvokeUnityToolsExecute(list, mode);

                if (resultObj == null)
                {
                    AppendLog($"Result: {mode} | Planned: {list.commands.Length} | Executed: 0");
                    AppendLog("[WARN] UnityTools.ExecuteCommands returned null.");
                    return;
                }

                // If it returned a string, treat it as a log message
                if (resultObj is string s)
                {
                    AppendLog($"Result: {mode} | Planned: {list.commands.Length} | Executed: (unknown)");
                    AppendLog("[LOG] " + s);
                    return;
                }

                // If it returned AiExecutionResult, print structured output
                if (resultObj is AiExecutionResult r)
                {
                    AppendLog($"Result: {r.mode} | Planned: {r.opsPlanned} | Executed: {r.opsExecuted}");

                    if (r.errors != null)
                        foreach (var e in r.errors) AppendLog("[ERROR] " + e);

                    if (r.warnings != null)
                        foreach (var w in r.warnings) AppendLog("[WARN] " + w);

                    if (r.logs != null)
                        foreach (var l in r.logs) AppendLog("[LOG] " + l);

                    return;
                }

                // Unknown return type
                AppendLog($"Result: {mode} | Planned: {list.commands.Length} | Executed: (unknown)");
                AppendLog("[WARN] UnityTools.ExecuteCommands returned unexpected type: " + resultObj.GetType().FullName);
            }
            catch (Exception ex)
            {
                AppendLog("[EXCEPTION] " + ex);
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// Tries to call UnityTools.ExecuteCommands with either:
        /// - ExecuteCommands(AiCommandList list, ExecutionMode mode)
        /// - ExecuteCommands(AiCommandList list)
        /// Returns whatever the tool returns (AiExecutionResult or string or null)
        /// </summary>
        private object InvokeUnityToolsExecute(AiCommandList list, ExecutionMode mode)
        {
            // UnityTools is expected in the same namespace AIAssistant
            var unityToolsType = typeof(AiAssistantWindow).Assembly.GetType("AIAssistant.UnityTools");
            if (unityToolsType == null)
                throw new Exception("Could not find type AIAssistant.UnityTools. Is UnityTools.cs in namespace AIAssistant?");

            // Prefer (AiCommandList, ExecutionMode)
            var m2 = unityToolsType.GetMethod(
                "ExecuteCommands",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(AiCommandList), typeof(ExecutionMode) },
                null
            );

            if (m2 != null)
                return m2.Invoke(null, new object[] { list, mode });

            // Fallback (AiCommandList)
            var m1 = unityToolsType.GetMethod(
                "ExecuteCommands",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(AiCommandList) },
                null
            );

            if (m1 != null)
                return m1.Invoke(null, new object[] { list });

            throw new Exception("UnityTools.ExecuteCommands overload not found. Expected ExecuteCommands(AiCommandList) or ExecuteCommands(AiCommandList, ExecutionMode).");
        }

        private void AppendLog(string message)
        {
            _log.Add(message);

            // Cap log size so editor stays responsive
            if (_log.Count > 300)
                _log.RemoveAt(0);

            Repaint();
        }
    }
}
