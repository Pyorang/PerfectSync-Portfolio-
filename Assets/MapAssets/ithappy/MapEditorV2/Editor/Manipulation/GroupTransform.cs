using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using ithappy.MapEditor;

namespace ithappy.MapEditor.Editor
{
    /// <summary>
    /// Utility for transforming groups of blocks together.
    /// </summary>
    public static class GroupTransform
    {
        /// <summary>
        /// Calculates the center position of a list of blocks.
        /// </summary>
        public static Vector3 GetGroupCenter(List<MapBlock> blocks)
        {
            if (blocks == null || blocks.Count == 0)
                return Vector3.zero;

            Vector3 sum = Vector3.zero;
            int validCount = 0;

            foreach (MapBlock block in blocks)
            {
                if (block != null)
                {
                    sum += block.transform.position;
                    validCount++;
                }
            }

            if (validCount == 0)
                return Vector3.zero;

            return sum / validCount;
        }

        /// <summary>
        /// Moves all blocks by a delta vector. Records undo.
        /// </summary>
        public static void MoveGroup(List<MapBlock> blocks, Vector3 delta)
        {
            if (blocks == null || blocks.Count == 0)
                return;

            foreach (MapBlock block in blocks)
            {
                if (block == null)
                    continue;

                Undo.RecordObject(block.transform, "Move Blocks");
                block.transform.position += delta;
            }
        }

        /// <summary>
        /// Rotates all blocks around the group center by the given angle on Y axis. Records undo.
        /// </summary>
        public static void RotateGroupAroundCenter(List<MapBlock> blocks, float yAngleDelta)
        {
            if (blocks == null || blocks.Count == 0)
                return;

            Vector3 center = GetGroupCenter(blocks);
            Quaternion rotation = Quaternion.Euler(0, yAngleDelta, 0);

            foreach (MapBlock block in blocks)
            {
                if (block == null)
                    continue;

                Undo.RecordObject(block.transform, "Rotate Blocks");

                // Rotate position around center
                Vector3 relativePos = block.transform.position - center;
                relativePos = rotation * relativePos;
                block.transform.position = center + relativePos;

                // Rotate the block itself
                block.transform.rotation *= rotation;
            }
        }

        /// <summary>
        /// Duplicates all blocks, offset by a given vector. Returns new blocks. Records undo.
        /// </summary>
        public static List<MapBlock> DuplicateGroup(List<MapBlock> blocks, Vector3 offset, MapRoot mapRoot)
        {
            List<MapBlock> newBlocks = new List<MapBlock>();

            if (blocks == null || blocks.Count == 0)
                return newBlocks;

            if (mapRoot == null)
            {
                Debug.LogError("Cannot duplicate blocks without a valid MapRoot.");
                return newBlocks;
            }

            foreach (MapBlock block in blocks)
            {
                if (block == null)
                    continue;

                GameObject duplicateGO = Object.Instantiate(
                    block.gameObject,
                    block.transform.position + offset,
                    block.transform.rotation,
                    block.transform.parent
                );

                MapBlock newBlock = duplicateGO.GetComponent<MapBlock>();
                if (newBlock != null)
                {
                    mapRoot.RegisterBlock(newBlock);
                    Undo.RegisterCreatedObjectUndo(duplicateGO, "Duplicate Blocks");
                    newBlocks.Add(newBlock);
                }
            }

            return newBlocks;
        }

        /// <summary>
        /// Deletes all blocks. Records undo.
        /// </summary>
        public static void DeleteGroup(List<MapBlock> blocks, MapRoot mapRoot)
        {
            if (blocks == null || blocks.Count == 0)
                return;

            if (mapRoot == null)
            {
                Debug.LogError("Cannot delete blocks without a valid MapRoot.");
                return;
            }

            // Create a copy to avoid modifying the list during iteration
            List<MapBlock> blocksCopy = new List<MapBlock>(blocks);

            foreach (MapBlock block in blocksCopy)
            {
                if (block == null)
                    continue;

                mapRoot.UnregisterBlock(block);
                Undo.DestroyObjectImmediate(block.gameObject);
            }
        }
    }
}
