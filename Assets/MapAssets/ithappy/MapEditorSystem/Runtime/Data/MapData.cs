using System;
using System.Collections.Generic;
using UnityEngine;

namespace ithappy.MapEditor
{
    /// <summary>
    /// Serializable wrapper for Vector3 to enable JSON serialization with JsonUtility.
    /// </summary>
    [Serializable]
    public class SerializableVector3
    {
        public float x;
        public float y;
        public float z;

        /// <summary>
        /// Creates a SerializableVector3 from a Vector3.
        /// </summary>
        public SerializableVector3(Vector3 v)
        {
            x = v.x;
            y = v.y;
            z = v.z;
        }

        /// <summary>
        /// Converts this SerializableVector3 back to a Vector3.
        /// </summary>
        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Implicit conversion from Vector3 to SerializableVector3.
        /// </summary>
        public static implicit operator SerializableVector3(Vector3 v)
        {
            return new SerializableVector3(v);
        }

        /// <summary>
        /// Implicit conversion from SerializableVector3 to Vector3.
        /// </summary>
        public static implicit operator Vector3(SerializableVector3 sv)
        {
            return sv?.ToVector3() ?? Vector3.zero;
        }
    }

    /// <summary>
    /// Serializable data representing a single placed block instance.
    /// </summary>
    [Serializable]
    public class PlacedBlockData
    {
        /// <summary>
        /// Unique identifier for the block definition.
        /// </summary>
        public string blockId;

        /// <summary>
        /// World position of the block.
        /// </summary>
        public SerializableVector3 position;

        /// <summary>
        /// Rotation in Euler angles (degrees).
        /// </summary>
        public SerializableVector3 rotation;

        /// <summary>
        /// Local scale of the block.
        /// </summary>
        public SerializableVector3 scale;

        /// <summary>
        /// Placement order (used for sorting/visibility).
        /// </summary>
        public int sortOrder;

        /// <summary>
        /// Custom properties serialized as JSON string for flexibility.
        /// Compatible with JsonUtility via string storage.
        /// </summary>
        public string customPropertiesJson;

        public PlacedBlockData()
        {
            blockId = string.Empty;
            position = new SerializableVector3(Vector3.zero);
            rotation = new SerializableVector3(Vector3.zero);
            scale = new SerializableVector3(Vector3.one);
            sortOrder = 0;
            customPropertiesJson = "{}";
        }
    }

    /// <summary>
    /// Metadata describing the overall map.
    /// </summary>
    [Serializable]
    public class MapMetadata
    {
        /// <summary>
        /// Name of the map creator.
        /// </summary>
        public string author;

        /// <summary>
        /// Description of the map's purpose or theme.
        /// </summary>
        public string description;

        /// <summary>
        /// Difficulty rating from 1 (easiest) to 5 (hardest).
        /// </summary>
        public int difficulty;

        /// <summary>
        /// Estimated time to complete in seconds.
        /// </summary>
        public float estimatedTime;

        public MapMetadata()
        {
            author = string.Empty;
            description = string.Empty;
            difficulty = 1;
            estimatedTime = 0f;
        }
    }

    /// <summary>
    /// Complete serializable map data structure.
    /// Can be serialized to/from JSON using JsonUtility.
    /// </summary>
    [Serializable]
    public class MapData
    {
        /// <summary>
        /// Display name of the map.
        /// </summary>
        public string mapName;

        /// <summary>
        /// Version identifier for format compatibility.
        /// </summary>
        public string version = "1.0";

        /// <summary>
        /// ISO 8601 timestamp when this map was created.
        /// </summary>
        public string createdAt;

        /// <summary>
        /// ISO 8601 timestamp when this map was last modified.
        /// </summary>
        public string modifiedAt;

        /// <summary>
        /// Grid size for block snapping and alignment.
        /// </summary>
        public float gridSize = 2f;

        /// <summary>
        /// Collection of all placed block instances.
        /// </summary>
        public List<PlacedBlockData> blocks = new List<PlacedBlockData>();

        /// <summary>
        /// Metadata describing the map.
        /// </summary>
        public MapMetadata metadata;

        public MapData()
        {
            mapName = "Untitled";
            version = "1.0";
            createdAt = System.DateTime.Now.ToString("o");
            modifiedAt = System.DateTime.Now.ToString("o");
            gridSize = 2f;
            blocks = new List<PlacedBlockData>();
            metadata = new MapMetadata();
        }
    }
}
