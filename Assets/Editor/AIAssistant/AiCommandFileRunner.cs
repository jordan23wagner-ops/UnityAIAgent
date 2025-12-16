#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AIAssistant
{
    public static class AiCommandFileRunner
    {
        private const string IncomingFolder = "Assets/AICommands/Incoming";
        private const string ProcessedFolder = "Assets/AICommands/Processed";
        private const string FailedFolder = "Assets/AICommands/Failed";
        private const string ReportsFolder = "Assets/AIReports/Commands";

        [MenuItem("Tools/Abyssbound/AI Assistant/Run Latest Incoming (DryRun)")]
        public static void RunLatestIncomingDryRun()
        {
            RunLatestIncoming(ExecutionMode.DryRun);
        }

        [MenuItem("Tools/Abyssbound/AI Assistant/Run Latest Incoming (Apply)")]
        public static void RunLatestIncomingApply()
        {
            RunLatestIncoming(ExecutionMode.Apply);
        }

        public static void RunLatestIncoming(ExecutionMode mode)
        {
            var latest = FindLatestIncomingJson();
            if (string.IsNullOrWhiteSpace(latest))
            {
                Debug.LogWarning($"[AIFileRunner] No incoming JSON found in '{IncomingFolder}'.");
                return;
            }

            RunFile(latest, mode);
        }

        public static void RunFile(string assetPath, ExecutionMode mode)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return;

            var fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[AIFileRunner] File not found: {assetPath}");
                return;
            }

            string json = null;
            try
            {
                json = File.ReadAllText(fullPath);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return;
            }

            var fileNameNoExt = Path.GetFileNameWithoutExtension(assetPath);

            // Parse
            if (!AiCommandParser.TryParse(json, out var env, out var list, out var parseError))
            {
                Debug.LogError($"[AIFileRunner] Parse failed: {parseError}");
                WriteCommandReport(mode, fileNameNoExt, env?.requestId, assetPath, null, parseError);
                MoveToFolder(assetPath, FailedFolder);
                AssetDatabase.Refresh();
                return;
            }

            var correlationId = !string.IsNullOrWhiteSpace(env?.requestId) ? env.requestId : fileNameNoExt;

            // QA capture around execution
            AiQaConsoleCapture.StartSession("CommandRun", correlationId);

            AiExecutionResult result = null;
            string execException = null;

            try
            {
                if (env != null)
                    result = UnityTools.ExecuteCommands(env, mode);
                else
                    result = UnityTools.ExecuteCommands(list, mode);
            }
            catch (Exception ex)
            {
                execException = ex.ToString();
                Debug.LogException(ex);
            }
            finally
            {
                var qaPath = AiQaConsoleCapture.StopSessionAndExport();
                if (!string.IsNullOrWhiteSpace(qaPath))
                    Debug.Log($"[AIFileRunner] QA report: {qaPath}");
            }

            // Report
            var reportPath = WriteCommandReport(mode, fileNameNoExt, env?.requestId, assetPath, result, execException);
            if (!string.IsNullOrWhiteSpace(reportPath))
                Debug.Log($"[AIFileRunner] Command report: {reportPath}");

            bool success = result != null && result.success && (result.errors == null || result.errors.Count == 0) && string.IsNullOrWhiteSpace(execException);

            MoveToFolder(assetPath, success ? ProcessedFolder : FailedFolder);
            AssetDatabase.Refresh();
        }

        private static string FindLatestIncomingJson()
        {
            if (!AssetDatabase.IsValidFolder(IncomingFolder))
                return null;

            var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { IncomingFolder });
            if (guids == null || guids.Length == 0)
                return null;

            var candidates = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p != null && p.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (candidates.Length == 0)
                return null;

            string best = null;
            DateTime bestTime = DateTime.MinValue;

            for (int i = 0; i < candidates.Length; i++)
            {
                var p = candidates[i];
                var t = File.GetLastWriteTimeUtc(Path.GetFullPath(p));
                if (t > bestTime)
                {
                    bestTime = t;
                    best = p;
                }
            }

            return best;
        }

        [Serializable]
        private class CommandRunReport
        {
            public string schemaVersion = "1.0";
            public string timestampUtc;
            public string sourceFile;
            public string mode;
            public string requestId;
            public string parseError;
            public string executionException;
            public AiExecutionResult result;
        }

        private static string WriteCommandReport(ExecutionMode mode, string fileNameNoExt, string requestId, string sourceAssetPath, AiExecutionResult result, string errorOrException)
        {
            var ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff");
            var rid = !string.IsNullOrWhiteSpace(requestId) ? requestId : fileNameNoExt;
            var safeRid = Sanitize(rid);
            var outPath = $"{ReportsFolder}/{ts}_{safeRid}.json";

            var report = new CommandRunReport
            {
                timestampUtc = DateTime.UtcNow.ToString("o"),
                sourceFile = sourceAssetPath,
                mode = mode.ToString(),
                requestId = rid,
                parseError = result == null && !string.IsNullOrWhiteSpace(errorOrException) ? errorOrException : null,
                executionException = result != null && !string.IsNullOrWhiteSpace(errorOrException) ? errorOrException : null,
                result = result
            };

            var full = Path.GetFullPath(outPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));

            File.WriteAllText(full, JsonUtility.ToJson(report, true));
            return outPath;
        }

        private static void MoveToFolder(string assetPath, string destFolder)
        {
            if (!AssetDatabase.IsValidFolder(destFolder))
            {
                Debug.LogWarning($"[AIFileRunner] Destination folder missing: {destFolder}");
                return;
            }

            var fileName = Path.GetFileName(assetPath);
            var destPath = destFolder.TrimEnd('/') + "/" + fileName;

            // Avoid overwriting - suffix with timestamp
            if (File.Exists(Path.GetFullPath(destPath)))
            {
                var ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff");
                var nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                var ext = Path.GetExtension(fileName);
                destPath = destFolder.TrimEnd('/') + "/" + Sanitize(nameNoExt) + "_" + ts + ext;
            }

            var err = AssetDatabase.MoveAsset(assetPath, destPath);
            if (!string.IsNullOrWhiteSpace(err))
                Debug.LogError($"[AIFileRunner] MoveAsset failed: {err} ({assetPath} -> {destPath})");
        }

        private static string Sanitize(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return "_";

            foreach (var c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');

            return s;
        }

        public static void OpenReportsFolder()
        {
            var full = Path.GetFullPath("Assets/AIReports");
            EditorUtility.RevealInFinder(full);
        }
    }
}
#endif
