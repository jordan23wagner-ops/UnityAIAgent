using System;
using UnityEngine;

namespace AIAssistant
{
    public static class AiCommandParser
    {
        /// <summary>
        /// Try to parse JSON into either an AiCommandEnvelope (preferred) or AiCommandList (fallback).
        /// Preserves existing AiAssistantWindow behavior.
        /// </summary>
        public static bool TryParse(string json, out AiCommandEnvelope env, out AiCommandList list, out string error)
        {
            env = null;
            list = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Empty JSON.";
                return false;
            }

            // Try envelope first.
            try { env = JsonUtility.FromJson<AiCommandEnvelope>(json); }
            catch { env = null; }

            if (env != null && env.commands != null && env.commands.commands != null && env.commands.commands.Length > 0)
            {
                list = env.commands;
                return true;
            }

            // Fallback: list-only JSON.
            try { list = JsonUtility.FromJson<AiCommandList>(json); }
            catch (Exception ex)
            {
                list = null;
                error = "Failed to parse AiCommandList JSON: " + ex.Message;
                return false;
            }

            if (list == null || list.commands == null || list.commands.Length == 0)
            {
                error = "Parsed no commands. Expected either: {\"schemaVersion\":...,\"commands\":{\"commands\":[...]}} or {\"commands\":[...]}";
                return false;
            }

            return true;
        }
    }
}
