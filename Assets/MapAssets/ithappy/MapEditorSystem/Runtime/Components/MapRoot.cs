using System.Collections.Generic;
using UnityEngine;

namespace ithappy.MapEditor
{
    /// <summary>
    /// Root container MonoBehaviour that manages an entire map.
    /// Handles registration/unregistration of blocks, import/export of map data,
    /// and provides query methods for finding blocks.
    /// </summary>
    public class MapRoot : MonoBehaviour
    {
        /// <summary>
        /// Display name for this map.
        /// </summary>
        [SerializeField]
        private string mapName = "Untitled";

        /// <summary>
        /// Grid size for block alignment.
        /// </summary>
        [SerializeField]
        private float gridSize = 2f;

        /// <summary>
        /// List of all placed blocks in this map.
        /// </summary>
        [SerializeField]
        private List<MapBlock> placedBlocks = new List<MapBlock>();

        /// <summary>
        /// Gets the total number of placed blocks.
        /// </summary>
        public int BlockCount => placedBlocks.Count;

        /// <summary>
        /// Gets a read-only view of all placed blocks.
        /// </summary>
        public IReadOnlyList<MapBlock> PlacedBlocks => placedBlocks.AsReadOnly();

        /// <summary>
        /// Gets the map name.
        /// </summary>
        public string MapName => mapName;

        /// <summary>
        /// Gets the grid size.
        /// </summary>
        public float GridSize => gridSize;

        /// <summary>
        /// Registers a newly placed block and assigns it a placement order.
        /// </summary>
        public void RegisterBlock(MapBlock block)
        {
            if (block == null)
            {
                Debug.LogError("Cannot register null MapBlock.", this);
                return;
            }

            if (placedBlocks.Contains(block))
            {
                Debug.LogWarning($"Block {block.gameObject.name} is already registered.", this);
                return;
            }

            block.SetPlacementOrder(placedBlocks.Count);
            placedBlocks.Add(block);
        }

        /// <summary>
        /// Unregisters a block and removes it from the list.
        /// </summary>
        public void UnregisterBlock(MapBlock block)
        {
            if (block == null)
            {
                Debug.LogError("Cannot unregister null MapBlock.", this);
                return;
            }

            if (!placedBlocks.Contains(block))
            {
                Debug.LogWarning($"Block {block.gameObject.name} is not registered.", this);
                return;
            }

            placedBlocks.Remove(block);
        }

        /// <summary>
        /// Destroys all placed blocks and clears the list.
        /// Uses DestroyImmediate in editor mode, Destroy in runtime.
        /// </summary>
        public void ClearAllBlocks()
        {
            // Create a copy to avoid modifying the list during iteration
            List<MapBlock> blocksCopy = new List<MapBlock>(placedBlocks);
            placedBlocks.Clear();

            foreach (MapBlock block in blocksCopy)
            {
                if (block == null)
                    continue;

#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(block.gameObject);
                }
                else
                {
                    Destroy(block.gameObject);
                }
#else
                Destroy(block.gameObject);
#endif
            }
        }

        /// <summary>
        /// Exports all placed blocks to a MapData structure.
        /// </summary>
        public MapData ExportToMapData()
        {
            var mapData = new MapData
            {
                mapName = mapName,
                gridSize = gridSize,
                createdAt = System.DateTime.Now.ToString("o"),
                modifiedAt = System.DateTime.Now.ToString("o")
            };

            foreach (MapBlock block in placedBlocks)
            {
                if (block != null)
                {
                    mapData.blocks.Add(block.ToData());
                }
            }

            return mapData;
        }

        /// <summary>
        /// Imports map data, instantiating blocks from a BlockDatabase.
        /// Clears existing blocks before importing.
        /// </summary>
        public void ImportFromMapData(MapData data, BlockDatabase database)
        {
            if (data == null)
            {
                Debug.LogError("Cannot import null MapData.", this);
                return;
            }

            if (database == null)
            {
                Debug.LogError("Cannot import without a BlockDatabase.", this);
                return;
            }

            // Clear existing blocks
            ClearAllBlocks();

            // Update map properties
            mapName = data.mapName;
            gridSize = data.gridSize;

            // Instantiate each block from the data
            foreach (PlacedBlockData blockData in data.blocks)
            {
                if (blockData == null || string.IsNullOrEmpty(blockData.blockId))
                {
                    Debug.LogWarning("Skipping invalid PlacedBlockData.", this);
                    continue;
                }

                // Find the definition in the database
                BlockDefinition definition = database.GetById(blockData.blockId);
                if (definition == null)
                {
                    Debug.LogWarning($"Block definition not found for ID: {blockData.blockId}", this);
                    continue;
                }

                // Instantiate the block prefab
                GameObject blockInstance = Instantiate(
                    definition.Prefab,
                    blockData.position.ToVector3(),
                    Quaternion.Euler(blockData.rotation.ToVector3()),
                    transform
                );

                blockInstance.name = $"{definition.DisplayName}_{BlockCount}";

                // Apply the data to the MapBlock component
                MapBlock mapBlock = blockInstance.GetComponent<MapBlock>();
                if (mapBlock == null)
                {
                    mapBlock = blockInstance.AddComponent<MapBlock>();
                }

                mapBlock.SetBlockId(blockData.blockId);
                mapBlock.definition = definition;
                mapBlock.FromData(blockData);

                // Register the block
                RegisterBlock(mapBlock);
            }
        }

        /// <summary>
        /// Finds a block at the given grid position.
        /// </summary>
        public MapBlock FindBlockAt(Vector3Int gridPos)
        {
            foreach (MapBlock block in placedBlocks)
            {
                if (block != null && block.GridPosition == gridPos)
                {
                    return block;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets all blocks matching a specific category.
        /// </summary>
        public List<MapBlock> GetBlocksByCategory(BlockCategory category)
        {
            var result = new List<MapBlock>();

            foreach (MapBlock block in placedBlocks)
            {
                if (block != null && block.definition != null && block.definition.Category == category)
                {
                    result.Add(block);
                }
            }

            return result;
        }

        /// <summary>
        /// Editor validation: removes null entries from the placed blocks list.
        /// </summary>
        private void OnValidate()
        {
            placedBlocks.RemoveAll(block => block == null);
        }
    }
}
