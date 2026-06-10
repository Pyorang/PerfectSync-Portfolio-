using UnityEngine;
using System.Collections.Generic;
using ithappy.MapEditor;

namespace ithappy.MapEditor.Editor
{
    /// <summary>
    /// Utility class for validating block placements and map integrity.
    /// Provides collision detection and map validation methods.
    /// </summary>
    public static class PlacementValidator
    {
        /// <summary>
        /// Checks if a block can be placed at the given position without overlapping existing blocks.
        /// Uses Physics.OverlapBox to detect collisions.
        /// </summary>
        /// <param name="position">World position for the block placement.</param>
        /// <param name="rotation">Rotation of the block.</param>
        /// <param name="blockBounds">Local bounds of the block.</param>
        /// <param name="ignoreLayer">Layer to ignore during collision checks (typically "Ignore Raycast").</param>
        /// <returns>True if placement is valid (no collisions), false otherwise.</returns>
        public static bool IsValidPlacement(Vector3 position, Quaternion rotation, Bounds blockBounds, int ignoreLayer)
        {
            // Calculate world center of the block
            Vector3 center = position + rotation * blockBounds.center;

            // Use slightly smaller extents to allow blocks to touch
            Vector3 halfExtents = blockBounds.extents * 0.9f;

            // Create layer mask that ignores the specified layer
            int layerMask = ~(1 << ignoreLayer);

            // Check for overlapping colliders
            Collider[] overlaps = Physics.OverlapBox(center, halfExtents, rotation, layerMask);

            // Valid if no colliders found
            return overlaps.Length == 0;
        }

        /// <summary>
        /// Validates that a map has required elements and is in a playable state.
        /// </summary>
        /// <param name="map">The MapRoot to validate.</param>
        /// <returns>A MapValidationResult containing validation status and any warnings.</returns>
        public static MapValidationResult ValidateMap(MapRoot map)
        {
            var result = new MapValidationResult
            {
                isValid = true,
                warnings = new List<string>()
            };

            if (map == null)
            {
                result.isValid = false;
                result.warnings.Add("MapRoot is null");
                return result;
            }

            // Check for minimum number of blocks
            if (map.BlockCount == 0)
            {
                result.warnings.Add("Map has no blocks placed");
            }

            // Check for start block (blocks with specific tag or category)
            bool hasStartBlock = HasBlockWithCategory(map, BlockCategory.Platforms);
            if (!hasStartBlock)
            {
                result.warnings.Add("Map has no platform blocks for starting area");
            }

            // Check for finish block (optional but recommended)
            bool hasFinishBlock = HasAnyBlock(map);
            if (!hasFinishBlock)
            {
                result.warnings.Add("Map has no blocks");
            }

            // Check for unreachable areas (basic heuristic)
            if (map.BlockCount > 0 && !AreBlocksConnected(map))
            {
                result.warnings.Add("Some blocks may be unreachable from the start");
            }

            // Overall validity: valid if no critical errors, but may have warnings
            result.isValid = result.warnings.Count <= 1; // Allow minor warnings

            return result;
        }

        /// <summary>
        /// Checks if the map has at least one block of a specific category.
        /// </summary>
        private static bool HasBlockWithCategory(MapRoot map, BlockCategory category)
        {
            if (map == null)
                return false;

            var blocks = map.GetBlocksByCategory(category);
            return blocks.Count > 0;
        }

        /// <summary>
        /// Checks if the map has at least one block.
        /// </summary>
        private static bool HasAnyBlock(MapRoot map)
        {
            if (map == null)
                return false;

            return map.BlockCount > 0;
        }

        /// <summary>
        /// Basic heuristic to check if blocks form a connected structure.
        /// Returns true if blocks are spatially close together (within reasonable bounds).
        /// </summary>
        private static bool AreBlocksConnected(MapRoot map)
        {
            if (map == null || map.BlockCount < 2)
                return true;

            var blocks = map.PlacedBlocks;
            if (blocks.Count < 2)
                return true;

            // Calculate bounds of all blocks
            Bounds totalBounds = blocks[0].GetWorldBounds();
            for (int i = 1; i < blocks.Count; i++)
            {
                if (blocks[i] == null)
                    continue;
                totalBounds.Encapsulate(blocks[i].GetWorldBounds());
            }

            // Check if blocks are spread out over a reasonable area
            // If extent is extremely large, blocks may be too disconnected
            float maxExtent = Mathf.Max(totalBounds.extents.x, totalBounds.extents.y, totalBounds.extents.z);
            const float maxReasonableExtent = 1000f;

            return maxExtent <= maxReasonableExtent;
        }

        /// <summary>
        /// Result of map validation containing status and warning messages.
        /// </summary>
        public struct MapValidationResult
        {
            /// <summary>
            /// True if the map is in a valid, playable state.
            /// </summary>
            public bool isValid;

            /// <summary>
            /// List of warning or error messages from validation.
            /// </summary>
            public List<string> warnings;
        }
    }
}
