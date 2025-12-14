using UnityEngine;
using UnityEditor;

namespace AIAssistant
{
    /// <summary>
    /// ScriptableObject to store OpenAI configuration (API key, model name).
    /// </summary>
    public class OpenAIConfig : ScriptableObject
    {
        [Header("AI Assistant Strict Mode")]
        public bool strictMode = true;
        [Header("OpenAI Settings")]
        [Tooltip("Your OpenAI API key. Keep this private and do NOT commit it to public repos.")]
        public string apiKey = "PASTE_YOUR_KEY_HERE";

        [Tooltip("Chat model to use, e.g., gpt-4.1-mini or gpt-4.1.")]
        public string model = "gpt-4.1-mini";

        private const string AssetPath = "Assets/Editor/AIAssistant/OpenAIConfig.asset";

        /// <summary>
        /// Load or create the config asset.
        /// </summary>
        public static OpenAIConfig GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<OpenAIConfig>(AssetPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<OpenAIConfig>();
                AssetDatabase.CreateAsset(settings, AssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Created OpenAIConfig at " + AssetPath);
            }
            return settings;
        }
    }

    /// <summary>
    /// Adds menu item to open config.
    /// </summary>
    public static class OpenAIConfigMenu
    {
        [MenuItem("Tools/AI Assistant/OpenAI Settings")]
        public static void OpenSettings()
        {
            var settings = OpenAIConfig.GetOrCreateSettings();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }
}
