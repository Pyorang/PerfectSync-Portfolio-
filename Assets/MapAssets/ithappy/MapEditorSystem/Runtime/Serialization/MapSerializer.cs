using System;
using System.IO;
using UnityEngine;

namespace ithappy.MapEditor
{
    /// <summary>
    /// Utility class for serializing and deserializing map data to/from JSON files.
    /// Uses Unity's JsonUtility for compatibility and simplicity.
    /// </summary>
    public static class MapSerializer
    {
        /// <summary>
        /// Saves a map to a JSON file.
        /// Updates the modifiedAt timestamp before writing.
        /// </summary>
        public static void SaveToJson(MapRoot map, string filePath)
        {
            if (map == null)
            {
                Debug.LogError("Cannot save null MapRoot to JSON.", null);
                return;
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                Debug.LogError("File path cannot be null or empty.", null);
                return;
            }

            try
            {
                // Export map data
                MapData mapData = map.ExportToMapData();

                // Update timestamp
                mapData.modifiedAt = DateTime.Now.ToString("o");

                // Ensure directory exists
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Serialize to JSON
                string json = JsonUtility.ToJson(mapData, prettyPrint: true);

                // Write to file
                File.WriteAllText(filePath, json);

                Debug.Log($"Map saved successfully to: {filePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save map to {filePath}: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Loads a map from a JSON file.
        /// Returns null if the file doesn't exist or parsing fails.
        /// </summary>
        public static MapData LoadFromJson(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                Debug.LogError("File path cannot be null or empty.", null);
                return null;
            }

            try
            {
                // Check if file exists
                if (!File.Exists(filePath))
                {
                    Debug.LogWarning($"Map file not found: {filePath}", null);
                    return null;
                }

                // Read file contents
                string json = File.ReadAllText(filePath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    Debug.LogError($"Map file is empty: {filePath}", null);
                    return null;
                }

                // Deserialize from JSON
                MapData mapData = JsonUtility.FromJson<MapData>(json);

                if (mapData == null)
                {
                    Debug.LogError($"Failed to deserialize map data from {filePath}", null);
                    return null;
                }

                Debug.Log($"Map loaded successfully from: {filePath}");
                return mapData;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load map from {filePath}: {ex.Message}", null);
                return null;
            }
        }

        /// <summary>
        /// Serializes MapData to a JSON string.
        /// </summary>
        public static string SerializeToString(MapData data)
        {
            if (data == null)
            {
                Debug.LogError("Cannot serialize null MapData.", null);
                return null;
            }

            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                return json;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to serialize map data to string: {ex.Message}", null);
                return null;
            }
        }

        /// <summary>
        /// Deserializes MapData from a JSON string.
        /// Returns null if parsing fails.
        /// </summary>
        public static MapData DeserializeFromString(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogError("JSON string cannot be null or empty.", null);
                return null;
            }

            try
            {
                MapData mapData = JsonUtility.FromJson<MapData>(json);

                if (mapData == null)
                {
                    Debug.LogError("Failed to deserialize map data from JSON string.", null);
                    return null;
                }

                return mapData;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse JSON string: {ex.Message}", null);
                return null;
            }
        }
    }
}
