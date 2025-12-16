#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace AIAssistant
{
    public static class AiQaConsoleCapture
    {
        [Serializable]
        private class QaLogEntry
        {
            public string timestampUtc;
            public string logType;
            public string message;
            public string stackTrace;
        }

        [Serializable]
        private class QaCompilerMessage
        {
            public string timestampUtc;
            public string assemblyPath;
            public string messageType;
            public string message;
            public string file;
            public int line;
            public int column;
        }

        [Serializable]
        private class QaReport
        {
            public string schemaVersion = "1.0";
            public string name;
            public string correlationId;
            public string startedUtc;
            public string endedUtc;
            public List<QaLogEntry> logs = new();
            public List<QaCompilerMessage> compilerMessages = new();
        }

        private static QaReport _active;
        private static bool _subscribed;

        public static void StartSession(string name, string correlationId)
        {
            StopSessionInternal(false);

            _active = new QaReport
            {
                name = string.IsNullOrWhiteSpace(name) ? "Session" : name,
                correlationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId,
                startedUtc = DateTime.UtcNow.ToString("o"),
                endedUtc = null
            };

            Subscribe();
        }

        public static string StopSessionAndExport()
        {
            return StopSessionInternal(true);
        }

        private static void Subscribe()
        {
            if (_subscribed)
                return;

            Application.logMessageReceived += OnLog;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            _subscribed = true;
        }

        private static void Unsubscribe()
        {
            if (!_subscribed)
                return;

            Application.logMessageReceived -= OnLog;
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
            _subscribed = false;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (_active == null)
                return;

            _active.logs.Add(new QaLogEntry
            {
                timestampUtc = DateTime.UtcNow.ToString("o"),
                logType = type.ToString(),
                message = condition,
                stackTrace = stackTrace
            });
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            if (_active == null || messages == null)
                return;

            for (int i = 0; i < messages.Length; i++)
            {
                var m = messages[i];
                _active.compilerMessages.Add(new QaCompilerMessage
                {
                    timestampUtc = DateTime.UtcNow.ToString("o"),
                    assemblyPath = assemblyPath,
                    messageType = m.type.ToString(),
                    message = m.message,
                    file = m.file,
                    line = m.line,
                    column = m.column
                });
            }
        }

        private static string StopSessionInternal(bool export)
        {
            if (_active == null)
                return null;

            _active.endedUtc = DateTime.UtcNow.ToString("o");

            Unsubscribe();

            if (!export)
            {
                _active = null;
                return null;
            }

            var fileName = BuildFileName(_active.name, _active.correlationId);
            var assetPath = "Assets/AIReports/QA/" + fileName;
            var fullPath = Path.GetFullPath(assetPath);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            var json = JsonUtility.ToJson(_active, true);
            File.WriteAllText(fullPath, json);

            _active = null;

            AssetDatabase.Refresh();
            return assetPath;
        }

        private static string BuildFileName(string name, string correlationId)
        {
            var ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff");
            var safeName = Sanitize(name);
            var safeCorr = Sanitize(correlationId);
            return $"{ts}_{safeName}_{safeCorr}.json";
        }

        private static string Sanitize(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return "_";

            foreach (var c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');

            return s;
        }
    }
}
#endif
