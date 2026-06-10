using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ithappy.MapEditor;

namespace ithappy.MapEditor.Editor
{
    /// <summary>
    /// Editor utility for auto-generating BlockDefinition assets and populating the BlockDatabase
    /// by scanning a prefabs folder and inferring block properties from naming and folder structure.
    /// </summary>
    public class BlockDatabaseGenerator
    {
        // Path constants — adjust these if your folder structure differs
        private const string PREFAB_SOURCE_PATH = "Assets/ithappy/Platformer_2_Obstacles/Prefabs";
        private const string DEFINITION_OUTPUT_PATH = "Assets/ithappy/MapEditorSystem/Resources/BlockDefinitions";
        private const string DATABASE_PATH = "Assets/ithappy/MapEditorSystem/Resources/BlockDatabase.asset";

        /// <summary>
        /// Menu item: Auto-generates block definitions from prefabs and updates the BlockDatabase.
        /// </summary>
        [MenuItem("MapEditor/Auto Generate Block Database")]
        public static void GenerateBlockDatabase()
        {
            EditorUtility.DisplayProgressBar("Block Database Generator", "Scanning prefabs folder...", 0f);

            try
            {
                // Step 1: Scan prefabs and create/update definitions
                List<BlockDefinition> allDefinitions = ScanAndCreateDefinitions();

                EditorUtility.DisplayProgressBar("Block Database Generator", "Creating/updating database...", 0.8f);

                // Step 2: Create or update the BlockDatabase
                CreateOrUpdateBlockDatabase(allDefinitions);

                EditorUtility.DisplayProgressBar("Block Database Generator", "Saving assets...", 0.9f);

                // Step 3: Save all assets
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.ClearProgressBar();

                // Show results dialog
                EditorUtility.DisplayDialog(
                    "Block Database Generated",
                    $"Successfully processed {allDefinitions.Count} block definitions.\n\nDatabase saved to {DATABASE_PATH}",
                    "OK"
                );
            }
            catch (System.Exception ex)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Error", $"Failed to generate block database:\n{ex.Message}", "OK");
                Debug.LogError($"BlockDatabaseGenerator error: {ex}", null);
            }
        }

        /// <summary>
        /// Menu item: Regenerates thumbnails for all block definitions in the database.
        /// Uses AssetPreview to generate preview images.
        /// </summary>
        [MenuItem("MapEditor/Regenerate Thumbnails")]
        public static void RegenerateThumbnails()
        {
            BlockDatabase database = AssetDatabase.LoadAssetAtPath<BlockDatabase>(DATABASE_PATH);
            if (database == null)
            {
                EditorUtility.DisplayDialog("Error", "BlockDatabase not found at " + DATABASE_PATH, "OK");
                return;
            }

            List<BlockDefinition> definitions = database.GetAllBlocks();
            if (definitions.Count == 0)
            {
                EditorUtility.DisplayDialog("Info", "No block definitions to process.", "OK");
                return;
            }

            int processedCount = 0;
            foreach (BlockDefinition def in definitions)
            {
                if (def == null || def.Prefab == null)
                    continue;

                EditorUtility.DisplayProgressBar(
                    "Regenerating Thumbnails",
                    $"Processing {def.BlockId}...",
                    (float)processedCount / definitions.Count
                );

                // Get asset preview - may return null on first call
                Texture2D preview = AssetPreview.GetAssetPreview(def.Prefab);
                if (preview != null)
                {
                    // Use reflection to set the thumbnail property if needed
                    SerializedObject so = new SerializedObject(def);
                    SerializedProperty thumbProp = so.FindProperty("thumbnail");
                    if (thumbProp != null)
                    {
                        thumbProp.objectReferenceValue = preview;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(def);
                    }
                }

                processedCount++;
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Success", $"Regenerated thumbnails for {processedCount} blocks.", "OK");
        }

        /// <summary>
        /// Menu item: Selects and pings the BlockDatabase in the project.
        /// </summary>
        [MenuItem("MapEditor/Select Block Database")]
        public static void SelectBlockDatabase()
        {
            BlockDatabase database = AssetDatabase.LoadAssetAtPath<BlockDatabase>(DATABASE_PATH);
            if (database == null)
            {
                EditorUtility.DisplayDialog("Error", "BlockDatabase not found at " + DATABASE_PATH, "OK");
                return;
            }

            Selection.activeObject = database;
            EditorGUIUtility.PingObject(database);
        }

        /// <summary>
        /// Scans the prefabs folder recursively and creates or updates BlockDefinition assets.
        /// Maps folder names to BlockCategory enum values.
        /// </summary>
        /// <returns>A list of all processed BlockDefinition objects.</returns>
        private static List<BlockDefinition> ScanAndCreateDefinitions()
        {
            List<BlockDefinition> allDefinitions = new List<BlockDefinition>();

            if (!Directory.Exists(PREFAB_SOURCE_PATH))
            {
                Debug.LogError($"Prefabs source path not found: {PREFAB_SOURCE_PATH}");
                return allDefinitions;
            }

            // Get all prefab files recursively
            string[] prefabGUIDs = AssetDatabase.FindAssets("t:Prefab", new[] { PREFAB_SOURCE_PATH });
            int processedCount = 0;

            foreach (string guid in prefabGUIDs)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                processedCount++;

                EditorUtility.DisplayProgressBar(
                    "Block Database Generator",
                    $"Processing {Path.GetFileNameWithoutExtension(assetPath)}...",
                    (float)processedCount / prefabGUIDs.Length * 0.8f
                );

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null)
                    continue;

                // Infer category from folder structure
                BlockCategory category = InferCategoryFromPath(assetPath);

                // Create or get existing definition
                BlockDefinition definition = CreateOrGetBlockDefinition(prefab, category);
                if (definition != null)
                {
                    allDefinitions.Add(definition);
                }
            }

            return allDefinitions;
        }

        /// <summary>
        /// Creates a new BlockDefinition or returns an existing one for the given prefab.
        /// </summary>
        private static BlockDefinition CreateOrGetBlockDefinition(GameObject prefab, BlockCategory category)
        {
            if (prefab == null)
                return null;

            string blockId = prefab.name;
            string definitionFolder = Path.Combine(DEFINITION_OUTPUT_PATH, category.ToString());
            string definitionPath = Path.Combine(definitionFolder, blockId + ".asset");

            // Check if definition already exists
            BlockDefinition existingDef = AssetDatabase.LoadAssetAtPath<BlockDefinition>(definitionPath);
            if (existingDef != null)
            {
                return existingDef;
            }

            // Create directories if needed
            if (!Directory.Exists(DEFINITION_OUTPUT_PATH))
            {
                Directory.CreateDirectory(DEFINITION_OUTPUT_PATH);
            }

            if (!Directory.Exists(definitionFolder))
            {
                Directory.CreateDirectory(definitionFolder);
            }

            // Create new BlockDefinition
            BlockDefinition newDef = ScriptableObject.CreateInstance<BlockDefinition>();

            // Set properties via reflection since they're private
            SerializedObject so = new SerializedObject(newDef);

            // Set blockId
            SerializedProperty blockIdProp = so.FindProperty("blockId");
            if (blockIdProp != null)
            {
                blockIdProp.stringValue = blockId;
            }

            // Set displayName (format: convert underscores to spaces, title case)
            string displayName = FormatDisplayName(blockId);
            SerializedProperty displayNameProp = so.FindProperty("displayName");
            if (displayNameProp != null)
            {
                displayNameProp.stringValue = displayName;
            }

            // Set category
            SerializedProperty categoryProp = so.FindProperty("category");
            if (categoryProp != null)
            {
                categoryProp.enumValueIndex = (int)category;
            }

            // Set prefab reference
            SerializedProperty prefabProp = so.FindProperty("prefab");
            if (prefabProp != null)
            {
                prefabProp.objectReferenceValue = prefab;
            }

            // Calculate and set grid size from prefab bounds
            Vector3 gridSize = CalculateGridSizeFromBounds(prefab);
            SerializedProperty gridSizeProp = so.FindProperty("gridSize");
            if (gridSizeProp != null)
            {
                gridSizeProp.vector3Value = gridSize;
            }

            // Set tags
            string[] tags = ExtractTagsFromName(blockId);
            SerializedProperty tagsProp = so.FindProperty("tags");
            if (tagsProp != null)
            {
                tagsProp.arraySize = tags.Length;
                for (int i = 0; i < tags.Length; i++)
                {
                    tagsProp.GetArrayElementAtIndex(i).stringValue = tags[i];
                }
            }

            so.ApplyModifiedProperties();

            // Save the asset
            AssetDatabase.CreateAsset(newDef, definitionPath);
            AssetDatabase.SaveAssets();

            return newDef;
        }

        /// <summary>
        /// Creates or updates the BlockDatabase with all discovered definitions.
        /// </summary>
        private static void CreateOrUpdateBlockDatabase(List<BlockDefinition> definitions)
        {
            // Ensure directory exists
            string dbFolder = Path.GetDirectoryName(DATABASE_PATH);
            if (!Directory.Exists(dbFolder))
            {
                Directory.CreateDirectory(dbFolder);
            }

            BlockDatabase database = AssetDatabase.LoadAssetAtPath<BlockDatabase>(DATABASE_PATH);

            if (database == null)
            {
                // Create new database
                database = ScriptableObject.CreateInstance<BlockDatabase>();
                AssetDatabase.CreateAsset(database, DATABASE_PATH);
            }

            // Update the blocks list via reflection
            SerializedObject so = new SerializedObject(database);
            SerializedProperty blocksProp = so.FindProperty("blocks");

            if (blocksProp != null)
            {
                blocksProp.arraySize = definitions.Count;
                for (int i = 0; i < definitions.Count; i++)
                {
                    blocksProp.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
                }
            }

            so.ApplyModifiedProperties();

            // Call RefreshFromDefinitions to clean up duplicates and nulls
            database.RefreshFromDefinitions();

            EditorUtility.SetDirty(database);
        }

        /// <summary>
        /// Infers the BlockCategory from the asset path folder structure.
        /// </summary>
        private static BlockCategory InferCategoryFromPath(string assetPath)
        {
            string pathLower = assetPath.ToLower();

            if (pathLower.Contains("/platforms/"))
                return BlockCategory.Platforms;
            if (pathLower.Contains("/obstacles/"))
                return BlockCategory.Obstacles;
            if (pathLower.Contains("/walls/"))
                return BlockCategory.Walls;
            if (pathLower.Contains("/tubes/"))
                return BlockCategory.Tubes;
            if (pathLower.Contains("/interaction/"))
                return BlockCategory.Interaction;
            if (pathLower.Contains("/aircraft/"))
                return BlockCategory.Aircraft;
            if (pathLower.Contains("/props/"))
                return BlockCategory.Props;

            // Default to Platforms if no clear category
            return BlockCategory.Platforms;
        }

        /// <summary>
        /// Formats a block ID into a display name by replacing underscores with spaces and title-casing.
        /// </summary>
        private static string FormatDisplayName(string blockId)
        {
            // Replace underscores with spaces
            string formatted = blockId.Replace("_", " ");

            // Title case: capitalize first letter of each word
            System.Globalization.TextInfo textInfo = System.Globalization.CultureInfo.CurrentCulture.TextInfo;
            return textInfo.ToTitleCase(formatted.ToLower());
        }

        /// <summary>
        /// Extracts meaningful tags from the block ID.
        /// For example, "platform_001" yields ["platform"].
        /// </summary>
        private static string[] ExtractTagsFromName(string blockId)
        {
            // Split by underscores and numbers, take the non-numeric part
            string[] parts = blockId.Split('_');

            if (parts.Length > 0 && !string.IsNullOrEmpty(parts[0]))
            {
                // Remove trailing numbers from the first part
                string tag = Regex.Replace(parts[0], @"\d+$", "").ToLower();
                if (!string.IsNullOrEmpty(tag))
                {
                    return new[] { tag };
                }
            }

            return new string[0];
        }

        /// <summary>
        /// Calculates the grid size by examining the prefab's bounds and rounding up to the nearest grid unit.
        /// </summary>
        private static Vector3 CalculateGridSizeFromBounds(GameObject prefab)
        {
            if (prefab == null)
                return Vector3.one;

            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();

            if (renderers == null || renderers.Length == 0)
            {
                return Vector3.one;
            }

            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combinedBounds.Encapsulate(renderers[i].bounds);
            }

            // Round up to nearest 0.5 grid unit
            const float gridUnit = 0.5f;
            Vector3 size = combinedBounds.size;
            Vector3 roundedSize = new Vector3(
                Mathf.Ceil(size.x / gridUnit) * gridUnit,
                Mathf.Ceil(size.y / gridUnit) * gridUnit,
                Mathf.Ceil(size.z / gridUnit) * gridUnit
            );

            return roundedSize;
        }
    }
}
