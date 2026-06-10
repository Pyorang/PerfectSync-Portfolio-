using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace ithappy.MapEditor.Editor
{
    /// <summary>
    /// ScriptableObject that persists editor preferences and settings for the Map Editor.
    /// This asset is stored in the Resources folder and loaded by the editor window.
    /// All changes are automatically saved to disk by Unity.
    /// </summary>
    [CreateAssetMenu(fileName = "MapEditorSettings", menuName = "MapEditor/Editor Settings", order = 2)]
    public class MapEditorSettings : ScriptableObject
    {
        [Header("Grid Settings")]
        public float defaultGridSize = 2f;
        public int gridExtent = 50;
        public float rotationSnapAngle = 15f;
        public float scaleSnapUnit = 0.25f;
        public bool showGridByDefault = true;
        public bool snapByDefault = true;

        [Header("Visualization")]
        public Color gridColor = new Color(1f, 1f, 1f, 0.15f);
        public Color ghostColor = new Color(0.3f, 0.8f, 1f, 0.5f);
        public Color selectionColor = new Color(1f, 0.8f, 0f, 0.8f);
        public float ghostTransparency = 0.5f;

        [Header("Keyboard Shortcuts")]
        public KeyCode rotateKey = KeyCode.R;
        public KeyCode toggleGridKey = KeyCode.G;
        public KeyCode toggleSnapKey = KeyCode.S;

        [Header("File Management")]
        public string defaultSavePath = "Assets/Maps";

        [Header("References")]
        public BlockDatabase blockDatabase;

        // Static instance cache for quick access
        private static MapEditorSettings _cachedSettings;

        /// <summary>
        /// Gets the current MapEditorSettings, creating one if it doesn't exist.
        /// The settings are loaded from Resources/MapEditorSettings or created at the default location.
        /// This method uses caching to avoid repeated asset database lookups.
        /// </summary>
        /// <returns>The MapEditorSettings instance</returns>
        public static MapEditorSettings GetOrCreateSettings()
        {
#if UNITY_EDITOR
            // Return cached instance if available
            if (_cachedSettings != null)
                return _cachedSettings;

            // Try to load from Resources folder
            _cachedSettings = Resources.Load<MapEditorSettings>("MapEditorSettings");

            if (_cachedSettings != null)
                return _cachedSettings;

            // Settings don't exist, create a new one
            _cachedSettings = CreateInstance<MapEditorSettings>();

            // Create the directory structure if needed
            string resourcesPath = "Assets/ithappy/MapEditorSystem/Resources";
            if (!AssetDatabase.IsValidFolder(resourcesPath))
            {
                string ithappyPath = "Assets/ithappy";
                if (!AssetDatabase.IsValidFolder(ithappyPath))
                {
                    AssetDatabase.CreateFolder("Assets", "ithappy");
                }
                string parentPath = "Assets/ithappy/MapEditorSystem";
                if (!AssetDatabase.IsValidFolder(parentPath))
                {
                    AssetDatabase.CreateFolder(ithappyPath, "MapEditorSystem");
                }
                AssetDatabase.CreateFolder(parentPath, "Resources");
            }

            // Save the new settings asset
            string assetPath = "Assets/ithappy/MapEditorSystem/Resources/MapEditorSettings.asset";
            AssetDatabase.CreateAsset(_cachedSettings, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return _cachedSettings;
#else
            // Fallback for runtime (shouldn't normally happen)
            return Resources.Load<MapEditorSettings>("MapEditorSettings");
#endif
        }

        /// <summary>
        /// Marks the settings as dirty so changes are saved to disk.
        /// Call this after modifying settings programmatically.
        /// </summary>
        public void MarkDirty()
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
#endif
        }

        /// <summary>
        /// Resets all settings to their default values.
        /// </summary>
        public void ResetToDefaults()
        {
            defaultGridSize = 2f;
            rotationSnapAngle = 15f;
            scaleSnapUnit = 0.25f;
            showGridByDefault = true;
            snapByDefault = true;
            gridExtent = 50;
            gridColor = new Color(1f, 1f, 1f, 0.15f);
            ghostColor = new Color(0.3f, 0.8f, 1f, 0.5f);
            selectionColor = new Color(1f, 0.8f, 0f, 0.8f);
            ghostTransparency = 0.5f;
            rotateKey = KeyCode.R;
            toggleGridKey = KeyCode.G;
            toggleSnapKey = KeyCode.S;
            defaultSavePath = "Assets/Maps";
            blockDatabase = null;

            MarkDirty();
        }

        /// <summary>
        /// Called when the settings asset is loaded to clear the cache.
        /// This ensures fresh settings are always available after asset reloads.
        /// </summary>
        private void OnEnable()
        {
            // Keep the static reference updated
            if (_cachedSettings == null)
                _cachedSettings = this;
        }
    }
}
