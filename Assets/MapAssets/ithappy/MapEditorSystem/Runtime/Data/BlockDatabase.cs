using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ithappy.MapEditor
{
    /// <summary>
    /// ScriptableObject that serves as the central database for all block definitions.
    /// Provides query and lookup methods for efficient block retrieval.
    /// </summary>
    [CreateAssetMenu(fileName = "BlockDatabase", menuName = "MapEditor/Block Database", order = 1)]
    public class BlockDatabase : ScriptableObject
    {
        [SerializeField]
        private List<BlockDefinition> blocks = new List<BlockDefinition>();

        /// <summary>
        /// Gets the total number of block definitions in this database.
        /// </summary>
        public int Count => blocks?.Count ?? 0;

        /// <summary>
        /// Retrieves all block definitions that belong to the specified category.
        /// </summary>
        /// <param name="category">The block category to filter by.</param>
        /// <returns>A list of BlockDefinition objects in the given category. Empty list if none found.</returns>
        public List<BlockDefinition> GetByCategory(BlockCategory category)
        {
            if (blocks == null || blocks.Count == 0)
            {
                return new List<BlockDefinition>();
            }

            return blocks
                .Where(block => block != null && block.Category == category)
                .ToList();
        }

        /// <summary>
        /// Searches the database for blocks matching a keyword across their ID, display name, and tags.
        /// Search is case-insensitive.
        /// </summary>
        /// <param name="keyword">The search term. Searches blockId, displayName, and tags.</param>
        /// <returns>A list of matching BlockDefinition objects. Empty list if no matches found.</returns>
        public List<BlockDefinition> Search(string keyword)
        {
            if (blocks == null || blocks.Count == 0)
            {
                return new List<BlockDefinition>();
            }

            if (string.IsNullOrEmpty(keyword))
            {
                return new List<BlockDefinition>(blocks.Where(b => b != null));
            }

            string lowerKeyword = keyword.ToLower();

            return blocks
                .Where(block =>
                {
                    if (block == null)
                    {
                        return false;
                    }

                    // Search in block ID
                    if (block.BlockId != null && block.BlockId.ToLower().Contains(lowerKeyword))
                    {
                        return true;
                    }

                    // Search in display name
                    if (block.DisplayName != null && block.DisplayName.ToLower().Contains(lowerKeyword))
                    {
                        return true;
                    }

                    // Search in tags
                    if (block.Tags != null && block.Tags.Any(tag =>
                        !string.IsNullOrEmpty(tag) && tag.ToLower().Contains(lowerKeyword)))
                    {
                        return true;
                    }

                    return false;
                })
                .ToList();
        }

        /// <summary>
        /// Retrieves a block definition by its unique ID.
        /// </summary>
        /// <param name="blockId">The unique identifier of the block.</param>
        /// <returns>The BlockDefinition with the matching ID, or null if not found.</returns>
        public BlockDefinition GetById(string blockId)
        {
            if (blocks == null || blocks.Count == 0)
            {
                return null;
            }

            if (string.IsNullOrEmpty(blockId))
            {
                return null;
            }

            return blocks.FirstOrDefault(block => block != null && block.BlockId == blockId);
        }

        /// <summary>
        /// Gets all unique block categories represented in this database.
        /// </summary>
        /// <returns>An array of BlockCategory values present in the database. Empty array if database is empty.</returns>
        public BlockCategory[] GetAllCategories()
        {
            if (blocks == null || blocks.Count == 0)
            {
                return new BlockCategory[0];
            }

            return blocks
                .Where(block => block != null)
                .Select(block => block.Category)
                .Distinct()
                .ToArray();
        }

        /// <summary>
        /// Gets a read-only view of all blocks in the database.
        /// </summary>
        /// <returns>A list of all non-null BlockDefinition objects.</returns>
        public List<BlockDefinition> GetAllBlocks()
        {
            if (blocks == null)
            {
                return new List<BlockDefinition>();
            }

            return blocks.Where(block => block != null).ToList();
        }

#if UNITY_EDITOR

        /// <summary>
        /// Cleans up the database by removing null entries and duplicate block IDs.
        /// When duplicates are found, the first occurrence is kept.
        /// This method should only be called in the editor.
        /// </summary>
        public void RefreshFromDefinitions()
        {
            if (blocks == null)
            {
                blocks = new List<BlockDefinition>();
                return;
            }

            // Remove null entries
            blocks.RemoveAll(block => block == null);

            // Remove duplicates by blockId, keeping the first occurrence
            var seen = new HashSet<string>();
            var duplicateIndices = new List<int>();

            for (int i = 0; i < blocks.Count; i++)
            {
                if (string.IsNullOrEmpty(blocks[i].BlockId))
                {
                    duplicateIndices.Add(i);
                    continue;
                }

                if (seen.Contains(blocks[i].BlockId))
                {
                    duplicateIndices.Add(i);
                }
                else
                {
                    seen.Add(blocks[i].BlockId);
                }
            }

            // Remove duplicates in reverse order to maintain correct indices
            for (int i = duplicateIndices.Count - 1; i >= 0; i--)
            {
                blocks.RemoveAt(duplicateIndices[i]);
            }

            // Mark the asset as modified
            UnityEditor.EditorUtility.SetDirty(this);
        }

#endif
    }
}
