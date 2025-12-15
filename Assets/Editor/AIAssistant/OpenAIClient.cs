using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using AiAssistant;

namespace AIAssistant
{
    /// <summary>
    /// OpenAI client that turns natural language into AiCommandList JSON.
    /// Supports all current actions handled by UnityTools:
    /// - create_primitive
    /// - create_prefab
    /// - move_object
    /// - rotate_object
    /// - scale_object
    /// - delete_object
    /// - rename_object
    /// - scatter_prefabs
    /// - scatter_primitives
    /// - place_in_grid
    /// - place_in_circle
    /// - place_in_ring
    /// - place_in_line
    /// </summary>
    public static class OpenAIClient
    {
        private const string ApiUrl = "https://api.openai.com/v1/chat/completions";

#pragma warning disable CS0649 // Fields are assigned by JSON deserialization.

        [Serializable]
        private class ChatMessage
        {
            public string role;
            public string content;
        }

        [Serializable]
        private class ChatRequest
        {
            public string model;
            public float temperature;
            public ChatMessage[] messages;
        }

        [Serializable]
        private class ChatResponse
        {
            public Choice[] choices;
        }

        [Serializable]
        private class Choice
        {
            public Message message;
        }

        [Serializable]
        private class Message
        {
            public string content;
        }

#pragma warning restore CS0649

        /// <summary>
        /// Main entry point: send user prompt to OpenAI and parse back AiCommandList.
        /// </summary>
        public static AiCommandList GetCommandsFromPrompt(
            string userPrompt,
            OpenAIConfig config,
            out string rawContent,
            out string error)
        {
            rawContent = null;
            error = null;

            if (config == null)
            {
                error = "OpenAIConfig is null.";
                return null;
            }

            if (string.IsNullOrEmpty(config.apiKey))
            {
                error = "OpenAI API key is empty. Set it in Tools → AI Assistant → OpenAI Settings.";
                return null;
            }

            // System prompt that teaches the model our schema and actions.
            string systemContent =
                "You are a Unity Editor command generator for a game.\n" +
                "The user will describe changes to make in the Unity scene.\n" +
                "You MUST respond ONLY with JSON, no explanations, no markdown, no comments.\n\n" +
                "The JSON must match this C#-style schema:\n\n" +
                "AiCommandList {\n" +
                "  AiCommand[] commands;\n" +
                "}\n" +
                "AiCommand {\n" +
                "  string action;          // one of: \"create_primitive\", \"create_prefab\", \"move_object\", \"rotate_object\", \"scale_object\", \"delete_object\", \"rename_object\", \"scatter_prefabs\", \"scatter_primitives\", \"place_in_grid\", \"place_in_circle\", \"place_in_ring\", \"place_in_line\"\n" +
                "  string primitiveType;   // for create_primitive or scatter_primitives: \"Cube\", \"Sphere\", \"Capsule\", \"Plane\"\n" +
                "  string prefabName;      // for create_prefab, scatter_prefabs, place_in_circle, place_in_ring, place_in_line\n" +
                "  string targetName;      // for move/rotate/scale/delete/rename: name of existing GameObject\n" +
                "  string newName;         // for rename_object\n" +
                "  string namePattern;     // for batch placement, can include {i}\n" +
                "  int count;              // number of objects to create in scatter or patterns\n" +
                "  float spacing;          // spacing for grids or lines\n" +
                "  float radius;           // outer radius for circles, rings, or scatter\n" +
                "  float innerRadius;      // inner radius for rings\n" +
                "  float randomness;       // extra random offset for scatter\n" +
                "  Vector3Data position;       // { x, y, z } absolute center position\n" +
                "  Vector3Data rotationEuler;  // { x, y, z } absolute rotation\n" +
                "  Vector3Data scale;          // { x, y, z } absolute scale\n" +
                "  Vector3Data deltaPosition;       // { x, y, z } relative movement\n" +
                "  Vector3Data deltaRotationEuler;  // { x, y, z } relative rotation\n" +
                "  Vector3Data deltaScale;          // { x, y, z } relative scale multiplier\n" +
                "  int gridRows;   // for place_in_grid\n" +
                "  int gridCols;   // for place_in_grid\n" +
                "  Vector3Data startPos;   // for place_in_line\n" +
                "  Vector3Data endPos;     // for place_in_line\n" +
                "}\n\n" +
                "Valid actions and semantics:\n" +
                "- create_primitive: create a single primitive. Use primitiveType, position, scale, name.\n" +
                "- create_prefab: instantiate a prefab by prefabName. Use position, scale, name.\n" +
                "- move_object: move an existing object. Use targetName and either deltaPosition (relative) or position (absolute).\n" +
                "- rotate_object: rotate an existing object. Use targetName and either deltaRotationEuler or rotationEuler.\n" +
                "- scale_object: scale an existing object. Use targetName and either deltaScale (multiplicative) or scale (absolute).\n" +
                "- delete_object: delete a GameObject. Use targetName.\n" +
                "- rename_object: rename a GameObject. Use targetName and newName.\n" +
                "- scatter_prefabs: instantiate many prefab instances around a center. Use prefabName, count, position, radius, randomness, namePattern.\n" +
                "- scatter_primitives: create many primitive objects around a center. Use primitiveType, count, position, radius, namePattern.\n" +
                "- place_in_grid: create a grid of primitives. Use primitiveType, gridRows, gridCols, spacing, position, namePattern.\n" +
                "- place_in_circle: place prefabs (or fallback cubes) evenly around a circle. Use prefabName, count, radius, position, namePattern.\n" +
                "- place_in_ring: place prefabs randomly between innerRadius and radius around a center. Use prefabName, count, innerRadius, radius, position, namePattern.\n" +
                "- place_in_line: place prefabs along a line segment. Use prefabName, count, startPos, endPos, namePattern.\n\n" +
                "Interpret natural language in terms of these actions. Examples:\n" +
                "- \"scatter 30 rocks around here\" -> scatter_primitives\n" +
                "- \"scatter 20 tree prefabs around the player\" -> scatter_prefabs\n" +
                "- \"make a 10 by 10 floor tile grid\" -> place_in_grid\n" +
                "- \"place enemies in a circle around the boss\" -> place_in_circle\n" +
                "- \"create a ring of torches\" -> place_in_ring\n" +
                "- \"create 6 trees in a line from A to B\" -> place_in_line\n\n" +
                "When the user gives multiple instructions, break them into multiple AiCommand entries in commands.\n" +
                "ALWAYS respond with a single top-level JSON object of the form { \"commands\": [ ... ] } and nothing else.\n";

            ChatRequest requestObj = new ChatRequest
            {
                model = config.model,
                temperature = 0.2f,
                messages = new[]
                {
                    new ChatMessage { role = "system", content = systemContent },
                    new ChatMessage { role = "user", content = userPrompt }
                }
            };

            string requestJson = JsonUtility.ToJson(requestObj);

            using (UnityWebRequest www = new UnityWebRequest(ApiUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(requestJson);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("Authorization", "Bearer " + config.apiKey);

                var asyncOp = www.SendWebRequest();
                while (!asyncOp.isDone) { }

#if UNITY_2020_1_OR_NEWER
                if (www.result != UnityWebRequest.Result.Success)
#else
                if (www.isNetworkError || www.isHttpError)
#endif
                {
                    error = "OpenAI HTTP error: " + www.error;
                    return null;
                }

                string responseText = www.downloadHandler.text;

                ChatResponse responseObj;
                try
                {
                    responseObj = JsonUtility.FromJson<ChatResponse>(responseText);
                }
                catch (Exception ex)
                {
                    error = "Failed to parse OpenAI response JSON: " + ex.Message + "\nRaw: " + responseText;
                    return null;
                }

                if (responseObj == null || responseObj.choices == null || responseObj.choices.Length == 0)
                {
                    error = "OpenAI response has no choices.\nRaw: " + responseText;
                    return null;
                }

                var message = responseObj.choices[0].message;
                if (message == null || string.IsNullOrEmpty(message.content))
                {
                    error = "OpenAI response content is empty.\nRaw: " + responseText;
                    return null;
                }

                rawContent = message.content;

                string cleaned = StripMarkdownFences(rawContent);

                AiCommandList commands;
                try
                {
                    commands = JsonUtility.FromJson<AiCommandList>(cleaned);
                }
                catch (Exception ex)
                {
                    error = "Failed to parse AiCommandList JSON: " + ex.Message + "\nContent: " + cleaned;
                    return null;
                }

                return commands;
            }
        }

        /// <summary>
        /// If the model accidentally wraps JSON in ``` fences, strip them.
        /// </summary>
        private static string StripMarkdownFences(string content)
        {
            if (string.IsNullOrEmpty(content))
                return content;

            string trimmed = content.Trim();

            if (trimmed.StartsWith("```"))
            {
                int firstNewline = trimmed.IndexOf('\n');
                if (firstNewline >= 0 && firstNewline < trimmed.Length - 1)
                {
                    trimmed = trimmed.Substring(firstNewline + 1);
                }

                int lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
                if (lastFence >= 0)
                {
                    trimmed = trimmed.Substring(0, lastFence);
                }

                trimmed = trimmed.Trim();
            }

            return trimmed;
        }
    }
}
