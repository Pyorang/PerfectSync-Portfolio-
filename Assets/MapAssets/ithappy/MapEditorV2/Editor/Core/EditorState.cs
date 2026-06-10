using UnityEngine;
using System.Collections.Generic;
using ithappy.MapEditor;

namespace ithappy.MapEditor.Editor
{
    /// <summary>
    /// Defines the current operation mode of the map editor.
    /// </summary>
    public enum EditorMode
    {
        /// <summary>No active operation in progress.</summary>
        Idle,
        /// <summary>In block placement mode, waiting for click to place.</summary>
        Placing,
        /// <summary>One or more blocks are selected for manipulation.</summary>
        Selected,
        /// <summary>Moving selected blocks with mouse.</summary>
        Moving
    }

    /// <summary>
    /// Manages the editor's state machine and transitions between different modes.
    /// Tracks the current mode, selected blocks, rotation, and provides events for mode changes.
    /// </summary>
    public class EditorState
    {
        private EditorMode currentMode = EditorMode.Idle;
        private BlockDefinition selectedPrefab;
        private List<MapBlock> selectedBlocks = new List<MapBlock>();
        private float currentYRotation = 0f;

        private Dictionary<MapBlock, Vector3> originalPositions = new Dictionary<MapBlock, Vector3>();

        /// <summary>
        /// Gets the current editor mode.
        /// </summary>
        public EditorMode CurrentMode => currentMode;

        /// <summary>
        /// Gets the block definition selected for placement (valid in Placing mode).
        /// </summary>
        public BlockDefinition SelectedPrefab => selectedPrefab;

        /// <summary>
        /// Gets the list of currently selected blocks (valid in Selected/Moving modes).
        /// </summary>
        public List<MapBlock> SelectedBlocks => selectedBlocks;

        /// <summary>
        /// Gets the current Y-axis rotation in degrees (0-360).
        /// </summary>
        public float CurrentYRotation => currentYRotation;

        /// <summary>
        /// Fired when the editor mode changes. Arguments are (oldMode, newMode).
        /// </summary>
        public event System.Action<EditorMode, EditorMode> OnModeChanged;

        /// <summary>
        /// Enters Placing mode to prepare for block placement.
        /// Transition: Any mode → Placing
        /// </summary>
        /// <param name="block">The block definition to place.</param>
        public void EnterPlacingMode(BlockDefinition block)
        {
            if (block == null)
            {
                Debug.LogWarning("[EditorState] Cannot enter Placing mode with null BlockDefinition.");
                return;
            }

            EditorMode oldMode = currentMode;
            currentMode = EditorMode.Placing;
            selectedPrefab = block;
            selectedBlocks.Clear();
            currentYRotation = 0f;

            OnModeChanged?.Invoke(oldMode, currentMode);
        }

        /// <summary>
        /// Remains in Placing mode after a successful placement.
        /// Call this after placing a block to continue placing more blocks.
        /// </summary>
        public void PlaceAndContinue()
        {
            // Validate we're in Placing mode
            if (currentMode != EditorMode.Placing)
            {
                Debug.LogWarning("[EditorState] PlaceAndContinue called when not in Placing mode.");
                return;
            }

            // Reset rotation for next placement
            currentYRotation = 0f;
        }

        /// <summary>
        /// Enters Selected mode with a single block selected.
        /// Transition: Any mode → Selected
        /// </summary>
        /// <param name="block">The block to select.</param>
        public void EnterSelectedMode(MapBlock block)
        {
            if (block == null)
            {
                Debug.LogWarning("[EditorState] Cannot enter Selected mode with null MapBlock.");
                return;
            }

            EditorMode oldMode = currentMode;
            currentMode = EditorMode.Selected;
            selectedBlocks.Clear();
            selectedBlocks.Add(block);
            selectedPrefab = null;
            currentYRotation = 0f;

            OnModeChanged?.Invoke(oldMode, currentMode);
        }

        /// <summary>
        /// Adds a block to the current selection (multi-select).
        /// Only valid when already in Selected mode.
        /// </summary>
        /// <param name="block">The block to add to selection.</param>
        public void AddToSelection(MapBlock block)
        {
            if (block == null)
            {
                Debug.LogWarning("[EditorState] Cannot add null MapBlock to selection.");
                return;
            }

            if (currentMode != EditorMode.Selected)
            {
                Debug.LogWarning("[EditorState] AddToSelection called when not in Selected mode.");
                return;
            }

            if (!selectedBlocks.Contains(block))
            {
                selectedBlocks.Add(block);
            }
        }

        /// <summary>
        /// Removes a block from the current selection.
        /// If this was the last block, mode is NOT changed (caller should handle).
        /// </summary>
        /// <param name="block">The block to remove from selection.</param>
        public void RemoveFromSelection(MapBlock block)
        {
            if (block == null)
            {
                Debug.LogWarning("[EditorState] Cannot remove null MapBlock from selection.");
                return;
            }

            if (currentMode != EditorMode.Selected)
            {
                Debug.LogWarning("[EditorState] RemoveFromSelection called when not in Selected mode.");
                return;
            }

            selectedBlocks.Remove(block);
        }

        /// <summary>
        /// Enters Moving mode to begin moving selected blocks.
        /// Transition: Selected → Moving
        /// Captures original positions for cancel/restore.
        /// </summary>
        public void EnterMovingMode()
        {
            if (currentMode != EditorMode.Selected || selectedBlocks.Count == 0)
            {
                Debug.LogWarning("[EditorState] EnterMovingMode called when not in Selected mode or no blocks selected.");
                return;
            }

            EditorMode oldMode = currentMode;
            currentMode = EditorMode.Moving;

            // Store original positions for potential restore on cancel
            originalPositions.Clear();
            foreach (MapBlock block in selectedBlocks)
            {
                if (block != null)
                {
                    originalPositions[block] = block.transform.position;
                }
            }

            OnModeChanged?.Invoke(oldMode, currentMode);
        }

        /// <summary>
        /// Confirms the current move operation and returns to Idle.
        /// Transition: Moving → Idle
        /// </summary>
        public void ConfirmMove()
        {
            if (currentMode != EditorMode.Moving)
            {
                Debug.LogWarning("[EditorState] ConfirmMove called when not in Moving mode.");
                return;
            }

            EditorMode oldMode = currentMode;
            currentMode = EditorMode.Idle;
            selectedBlocks.Clear();
            originalPositions.Clear();
            currentYRotation = 0f;

            OnModeChanged?.Invoke(oldMode, currentMode);
        }

        /// <summary>
        /// Cancels the current move operation and restores original positions.
        /// Transition: Moving → Selected
        /// </summary>
        public void CancelMove()
        {
            if (currentMode != EditorMode.Moving)
            {
                Debug.LogWarning("[EditorState] CancelMove called when not in Moving mode.");
                return;
            }

            // Restore original positions
            foreach (var kvp in originalPositions)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.transform.position = kvp.Value;
                }
            }

            EditorMode oldMode = currentMode;
            currentMode = EditorMode.Selected;
            originalPositions.Clear();
            currentYRotation = 0f;

            OnModeChanged?.Invoke(oldMode, currentMode);
        }

        /// <summary>
        /// Cancels any active operation and returns to Idle.
        /// Clears all selections and resets rotation.
        /// Transition: Any mode → Idle
        /// </summary>
        public void Cancel()
        {
            if (currentMode == EditorMode.Moving)
            {
                CancelMove();
                return;
            }

            EditorMode oldMode = currentMode;
            currentMode = EditorMode.Idle;
            selectedBlocks.Clear();
            selectedPrefab = null;
            originalPositions.Clear();
            currentYRotation = 0f;

            if (oldMode != EditorMode.Idle)
            {
                OnModeChanged?.Invoke(oldMode, currentMode);
            }
        }

        /// <summary>
        /// Rotates the selected prefab or blocks 90 degrees clockwise (Y-axis, +90).
        /// Valid in Placing, Selected, and Moving modes.
        /// </summary>
        public void RotateCW()
        {
            if (currentMode == EditorMode.Idle)
            {
                Debug.LogWarning("[EditorState] RotateCW called in Idle mode.");
                return;
            }

            currentYRotation += 90f;
            if (currentYRotation >= 360f)
            {
                currentYRotation -= 360f;
            }
        }

        /// <summary>
        /// Rotates the selected prefab or blocks 90 degrees counter-clockwise (Y-axis, -90).
        /// Valid in Placing, Selected, and Moving modes.
        /// </summary>
        public void RotateCCW()
        {
            if (currentMode == EditorMode.Idle)
            {
                Debug.LogWarning("[EditorState] RotateCCW called in Idle mode.");
                return;
            }

            currentYRotation -= 90f;
            if (currentYRotation < 0f)
            {
                currentYRotation += 360f;
            }
        }

        /// <summary>
        /// Deletes all currently selected blocks.
        /// Valid in Selected mode only.
        /// </summary>
        public void DeleteSelected()
        {
            if (currentMode != EditorMode.Selected || selectedBlocks.Count == 0)
            {
                Debug.LogWarning("[EditorState] DeleteSelected called when not in Selected mode or no blocks selected.");
                return;
            }

            // Create a copy to safely iterate while deleting
            List<MapBlock> blocksCopy = new List<MapBlock>(selectedBlocks);
            selectedBlocks.Clear();

            foreach (MapBlock block in blocksCopy)
            {
                if (block != null)
                {
#if UNITY_EDITOR
                    if (!UnityEngine.Application.isPlaying)
                    {
                        Object.DestroyImmediate(block.gameObject);
                    }
                    else
                    {
                        Object.Destroy(block.gameObject);
                    }
#else
                    Object.Destroy(block.gameObject);
#endif
                }
            }

            // Return to Idle after deletion
            EditorMode oldMode = currentMode;
            currentMode = EditorMode.Idle;
            OnModeChanged?.Invoke(oldMode, currentMode);
        }

        /// <summary>
        /// Duplicates all currently selected blocks.
        /// Valid in Selected mode only.
        /// Creates new instances with the same properties and enters Selected mode with the new blocks.
        /// </summary>
        public void DuplicateSelected()
        {
            if (currentMode != EditorMode.Selected || selectedBlocks.Count == 0)
            {
                Debug.LogWarning("[EditorState] DuplicateSelected called when not in Selected mode or no blocks selected.");
                return;
            }

            List<MapBlock> newBlocks = new List<MapBlock>();

            foreach (MapBlock block in selectedBlocks)
            {
                if (block != null)
                {
                    // Create an offset so duplicates are visible
                    Vector3 offset = new Vector3(1f, 0f, 1f);

#if UNITY_EDITOR
                    if (!UnityEngine.Application.isPlaying)
                    {
                        GameObject duplicate = Object.Instantiate(
                            block.gameObject,
                            block.transform.position + offset,
                            block.transform.rotation,
                            block.transform.parent
                        );

                        MapBlock newBlock = duplicate.GetComponent<MapBlock>();
                        if (newBlock != null)
                        {
                            newBlocks.Add(newBlock);
                        }
                    }
                    else
                    {
                        GameObject duplicate = Object.Instantiate(
                            block.gameObject,
                            block.transform.position + offset,
                            block.transform.rotation,
                            block.transform.parent
                        );

                        MapBlock newBlock = duplicate.GetComponent<MapBlock>();
                        if (newBlock != null)
                        {
                            newBlocks.Add(newBlock);
                        }
                    }
#else
                    GameObject duplicate = Object.Instantiate(
                        block.gameObject,
                        block.transform.position + offset,
                        block.transform.rotation,
                        block.transform.parent
                    );

                    MapBlock newBlock = duplicate.GetComponent<MapBlock>();
                    if (newBlock != null)
                    {
                        newBlocks.Add(newBlock);
                    }
#endif
                }
            }

            // Update selection to the new duplicated blocks
            selectedBlocks.Clear();
            selectedBlocks.AddRange(newBlocks);
        }

        /// <summary>
        /// Checks if a specific block is currently selected.
        /// </summary>
        /// <param name="block">The block to check.</param>
        /// <returns>True if the block is in the current selection; false otherwise.</returns>
        public bool IsBlockSelected(MapBlock block)
        {
            if (block == null)
            {
                return false;
            }

            return selectedBlocks.Contains(block);
        }
    }
}
