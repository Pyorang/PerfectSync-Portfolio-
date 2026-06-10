using UnityEngine;
using UnityEditor;
using ithappy.MapEditor;

namespace ithappy.MapEditor.Editor
{
    /// <summary>
    /// Handles all Scene View input events and delegates to EditorState.
    /// Manages raycasting, snapping, and user interaction routing.
    /// Does not handle rendering or ghost previews — only input processing.
    /// </summary>
    public class SceneInteraction
    {
        private EditorState state;
        private SurfaceSnap surfaceSnap;
        private GhostPreview ghostPreview;

        /// <summary>
        /// Current snapped world position for block placement (updated on mouse move).
        /// </summary>
        public Vector3 CurrentSnappedPosition { get; private set; } = Vector3.zero;

        /// <summary>
        /// Normal vector of the surface at the snapped position.
        /// </summary>
        public Vector3 CurrentHitNormal { get; private set; } = Vector3.up;

        /// <summary>
        /// Current Y-level for placement plane when surface snap fails.
        /// </summary>
        public float CurrentYLevel { get; set; } = 0f;

        /// <summary>
        /// Callback for placing a block. Called by SceneInteraction when a placement occurs.
        /// Signature: (position, rotation, blockDefinition) → GameObject
        /// </summary>
        public System.Func<Vector3, Quaternion, BlockDefinition, GameObject> OnPlaceBlock { get; set; }

        /// <summary>
        /// Creates a new SceneInteraction handler.
        /// </summary>
        /// <param name="editorState">The EditorState to drive.</param>
        public SceneInteraction(EditorState editorState)
        {
            state = editorState;
            surfaceSnap = null;
            ghostPreview = null;
        }

        /// <summary>
        /// Sets an optional SurfaceSnap helper for advanced snapping logic.
        /// </summary>
        public void SetSurfaceSnap(SurfaceSnap snap)
        {
            surfaceSnap = snap;
        }

        /// <summary>
        /// Sets an optional GhostPreview helper for rendering previews.
        /// </summary>
        public void SetGhostPreview(GhostPreview preview)
        {
            ghostPreview = preview;
        }

        /// <summary>
        /// Main SceneView event handler. Call this from SceneView.duringSceneGui callback.
        /// Routes input based on current editor mode.
        /// </summary>
        public void OnSceneGUI(SceneView sceneView)
        {
            Event e = Event.current;

            // Always add default control in Layout pass to prevent Unity eating our input
            if (e.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                return;
            }

            // Route to handler based on current mode
            switch (state.CurrentMode)
            {
                case EditorMode.Idle:
                    HandleIdleMode(e, sceneView);
                    break;

                case EditorMode.Placing:
                    HandlePlacingMode(e, sceneView);
                    break;

                case EditorMode.Selected:
                    HandleSelectedMode(e, sceneView);
                    break;

                case EditorMode.Moving:
                    HandleMovingMode(e, sceneView);
                    break;
            }
        }

        /// <summary>
        /// Handles input in Idle mode.
        /// - Left click on MapBlock: select it
        /// - Left click on empty space: do nothing
        /// </summary>
        private void HandleIdleMode(Event e, SceneView sceneView)
        {
            if (e.type == EventType.MouseDown && e.button == 0) // Left click
            {
                MapBlock hitBlock = RaycastForMapBlock(e.mousePosition, sceneView);
                if (hitBlock != null)
                {
                    state.EnterSelectedMode(hitBlock);
                    e.Use();
                }
            }
        }

        /// <summary>
        /// Handles input in Placing mode.
        /// - Mouse move: update snapped position
        /// - Left click: place block, stay in Placing
        /// - Right click / ESC: cancel and return to Idle
        /// - R: rotate CW
        /// - Shift+R / T: rotate CCW
        /// - ScrollWheel: adjust Y level
        /// </summary>
        private void HandlePlacingMode(Event e, SceneView sceneView)
        {
            if (e.type == EventType.MouseMove)
            {
                UpdateSnappedPosition(e, sceneView);
                sceneView.Repaint();
            }
            else if (e.type == EventType.MouseDown && e.button == 0) // Left click
            {
                PlaceBlockAtSnappedPosition();
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 1) // Right click
            {
                state.Cancel();
                e.Use();
            }
            else if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Escape)
                {
                    state.Cancel();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.R && !e.shift)
                {
                    state.RotateCW();
                    e.Use();
                }
                else if ((e.keyCode == KeyCode.R && e.shift) || e.keyCode == KeyCode.T)
                {
                    state.RotateCCW();
                    e.Use();
                }
            }
            else if (e.type == EventType.ScrollWheel)
            {
                // Scroll down (e.delta.y < 0) decreases Y, scroll up (e.delta.y > 0) increases Y
                float scrollDelta = -e.delta.y * 0.1f;
                CurrentYLevel += scrollDelta;
                e.Use();
                sceneView.Repaint();
            }
        }

        /// <summary>
        /// Handles input in Selected mode.
        /// - Left click on another block: select it (replace selection)
        /// - Ctrl+left click on block: add to selection
        /// - Left click on empty: cancel
        /// - G: enter Moving mode
        /// - R: rotate CW
        /// - Delete: delete selected
        /// - Ctrl+D: duplicate selected
        /// - ESC: cancel
        /// </summary>
        private void HandleSelectedMode(Event e, SceneView sceneView)
        {
            if (e.type == EventType.MouseDown && e.button == 0) // Left click
            {
                MapBlock hitBlock = RaycastForMapBlock(e.mousePosition, sceneView);

                if (hitBlock != null)
                {
                    if (e.control)
                    {
                        // Ctrl+click: add to selection
                        if (state.IsBlockSelected(hitBlock))
                        {
                            state.RemoveFromSelection(hitBlock);
                        }
                        else
                        {
                            state.AddToSelection(hitBlock);
                        }
                    }
                    else
                    {
                        // Regular click: replace selection
                        state.EnterSelectedMode(hitBlock);
                    }
                }
                else
                {
                    // Click on empty space
                    state.Cancel();
                }

                e.Use();
            }
            else if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Escape)
                {
                    state.Cancel();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.G)
                {
                    state.EnterMovingMode();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.R && !e.shift && !e.control)
                {
                    state.RotateCW();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Delete)
                {
                    state.DeleteSelected();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.D && e.control)
                {
                    state.DuplicateSelected();
                    e.Use();
                }
            }
        }

        /// <summary>
        /// Handles input in Moving mode.
        /// - Mouse move: update block positions to follow mouse
        /// - Left click: confirm move
        /// - Right click / ESC: cancel move and restore positions
        /// - R: rotate CW
        /// </summary>
        private void HandleMovingMode(Event e, SceneView sceneView)
        {
            if (e.type == EventType.MouseMove)
            {
                UpdateSnappedPosition(e, sceneView);
                MoveSelectedBlocks();
                sceneView.Repaint();
            }
            else if (e.type == EventType.MouseDown && e.button == 0) // Left click
            {
                state.ConfirmMove();
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 1) // Right click
            {
                state.CancelMove();
                e.Use();
            }
            else if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Escape)
                {
                    state.CancelMove();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.R && !e.shift)
                {
                    state.RotateCW();
                    e.Use();
                }
            }
        }

        /// <summary>
        /// Raycasts from the mouse position to find a MapBlock collider.
        /// </summary>
        private MapBlock RaycastForMapBlock(Vector2 mousePos, SceneView sceneView)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);

            if (Physics.Raycast(ray, out RaycastHit hit, 10000f))
            {
                MapBlock mapBlock = hit.collider.GetComponentInParent<MapBlock>();
                return mapBlock;
            }

            return null;
        }

        /// <summary>
        /// Updates CurrentSnappedPosition and CurrentHitNormal based on raycast.
        /// Uses surface snap if available, otherwise falls back to Y-level plane intersection.
        /// </summary>
        private void UpdateSnappedPosition(Event e, SceneView sceneView)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            // Try surface snap via raycast against existing colliders
            if (Physics.Raycast(ray, out RaycastHit hit, 10000f))
            {
                CurrentSnappedPosition = hit.point;
                CurrentHitNormal = hit.normal;

                // If SurfaceSnap is available, let it refine the position
                if (surfaceSnap != null && state.SelectedPrefab != null)
                {
                    Bounds prefabBounds = new Bounds(Vector3.zero, state.SelectedPrefab.GridSize);
                    CurrentSnappedPosition = surfaceSnap.CalculateSnappedPosition(
                        ray,
                        prefabBounds
                    );
                }

                return;
            }

            // Fallback: intersect ray with Y-level plane
            CurrentSnappedPosition = CalculatePlacementPositionFallback(ray);
            CurrentHitNormal = Vector3.up;
        }

        /// <summary>
        /// Calculates placement position by intersecting the ray with the Y-level plane.
        /// </summary>
        private Vector3 CalculatePlacementPositionFallback(Ray ray)
        {
            float denom = ray.direction.y;
            if (Mathf.Abs(denom) < 0.0001f)
            {
                denom = 0.0001f;
            }

            float t = (CurrentYLevel - ray.origin.y) / denom;
            return ray.origin + ray.direction * Mathf.Max(0f, t);
        }

        /// <summary>
        /// Places a block at the current snapped position with the selected rotation.
        /// Calls OnPlaceBlock callback and then invokes state.PlaceAndContinue().
        /// </summary>
        private void PlaceBlockAtSnappedPosition()
        {
            if (state.SelectedPrefab == null || OnPlaceBlock == null)
            {
                Debug.LogWarning("[SceneInteraction] Cannot place block: no prefab selected or no placement callback.");
                return;
            }

            Quaternion rotation = Quaternion.Euler(0f, state.CurrentYRotation, 0f);

            // Invoke the placement callback
            GameObject placedInstance = OnPlaceBlock.Invoke(
                CurrentSnappedPosition,
                rotation,
                state.SelectedPrefab
            );

            if (placedInstance == null)
            {
                Debug.LogWarning("[SceneInteraction] Block placement callback returned null.");
                return;
            }

            // Stay in placing mode for continuous placement
            state.PlaceAndContinue();
        }

        /// <summary>
        /// Moves all selected blocks to follow the current snapped position.
        /// Maintains relative offsets and applies rotation.
        /// </summary>
        private void MoveSelectedBlocks()
        {
            if (state.SelectedBlocks.Count == 0)
            {
                return;
            }

            // Calculate center of selected blocks
            Vector3 centerPos = Vector3.zero;
            int validCount = 0;

            foreach (MapBlock block in state.SelectedBlocks)
            {
                if (block != null)
                {
                    centerPos += block.transform.position;
                    validCount++;
                }
            }

            if (validCount == 0)
            {
                return;
            }

            centerPos /= validCount;

            // Calculate offset from center to snap position
            Vector3 offset = CurrentSnappedPosition - centerPos;

            // Move all blocks by this offset
            foreach (MapBlock block in state.SelectedBlocks)
            {
                if (block != null)
                {
                    block.transform.position += offset;
                }
            }
        }
    }
}
