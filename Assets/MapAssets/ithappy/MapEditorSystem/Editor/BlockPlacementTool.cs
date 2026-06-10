using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ithappy.MapEditor;

namespace ithappy.MapEditor.Editor
{
    /// <summary>
    /// Handles block placement in the Scene View with grid snapping, rotation, and height control.
    /// This is a helper class used via SceneView.duringSceneGui callback from MapEditorWindow.
    /// Provides real-time ghost preview, keyboard shortcuts for rotation and height adjustment,
    /// and click-to-place functionality with undo support.
    /// </summary>
    public class BlockPlacementTool
    {
        // State fields
        private BlockDefinition currentBlock;
        private GameObject ghostInstance;
        private Vector3 ghostPosition = Vector3.zero;
        private float currentYRotation = 0f;
        private float currentYLevel = 0f;
        private Material ghostMaterial;
        private bool isActive = false;

        // Configuration
        private GridSnapSystem gridSnap;
        private MapEditorSettings settings;

        // Constants
        private const float GHOST_TRANSPARENCY = 0.3f;
        private const float LABEL_Y_OFFSET = 1.5f;
        private const KeyCode ROTATE_CW_KEY = KeyCode.R;
        private const KeyCode ROTATE_CCW_KEY = KeyCode.T;
        private const float ROTATION_STEP = 90f;
        private static readonly int RAYCAST_LAYER_MASK = ~0; // Raycast against all layers

        /// <summary>
        /// Initializes the placement tool with grid and settings references.
        /// </summary>
        public BlockPlacementTool(GridSnapSystem gridSnap, MapEditorSettings settings)
        {
            this.gridSnap = gridSnap;
            this.settings = settings;
        }

        /// <summary>
        /// Gets whether the placement tool is currently active.
        /// </summary>
        public bool IsActive => isActive;

        /// <summary>
        /// Activates block placement mode with the specified block definition.
        /// Creates a transparent ghost preview instance for real-time placement feedback.
        /// </summary>
        public void Activate(BlockDefinition block)
        {
            if (block == null || block.Prefab == null)
            {
                Debug.LogError("Cannot activate placement tool: block or prefab is null.", block);
                return;
            }

            currentBlock = block;
            currentYRotation = 0f;
            currentYLevel = gridSnap?.currentYLevel ?? 0f;
            isActive = true;

            // Create ghost preview instance
            ghostInstance = Object.Instantiate(block.Prefab);
            ghostInstance.name = $"_Ghost_{block.BlockId}";
            ghostInstance.hideFlags = HideFlags.HideAndDontSave;

            // Store original layer and set to IgnoreRaycast
            foreach (Transform child in ghostInstance.GetComponentsInChildren<Transform>())
            {
                child.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            }

            // Create and apply ghost material to all renderers
            ghostMaterial = CreateGhostMaterial();
            ApplyGhostMaterialToRenderers(ghostInstance);

            // Set initial position
            ghostPosition = Vector3.zero;
            ghostInstance.transform.position = ghostPosition;
        }

        /// <summary>
        /// Deactivates block placement mode and cleans up the ghost preview.
        /// </summary>
        public void Deactivate()
        {
            if (ghostInstance != null)
            {
                Object.DestroyImmediate(ghostInstance);
                ghostInstance = null;
            }

            if (ghostMaterial != null)
            {
                Object.DestroyImmediate(ghostMaterial);
                ghostMaterial = null;
            }

            currentBlock = null;
            isActive = false;
        }

        /// <summary>
        /// Main Scene View GUI handler. Called once per Scene View frame when placement is active.
        /// Handles input events, updates ghost position, and draws visual feedback.
        /// </summary>
        public void OnSceneGUI(SceneView sceneView)
        {
            if (!isActive || ghostInstance == null || currentBlock == null)
                return;

            HandleInput(sceneView);
            UpdateGhostPreview(sceneView);
            DrawVisualFeedback();
        }

        /// <summary>
        /// Handles keyboard and mouse input for block placement.
        /// </summary>
        private void HandleInput(SceneView sceneView)
        {
            Event evt = Event.current;

            switch (evt.type)
            {
                case EventType.MouseMove:
                case EventType.MouseDrag:
                    UpdateGhostPositionFromMouse(evt);
                    sceneView.Repaint();
                    break;

                case EventType.MouseDown:
                    if (evt.button == 0) // Left mouse button
                    {
                        PlaceBlock();
                        evt.Use();
                        sceneView.Repaint();
                    }
                    break;

                case EventType.KeyDown:
                    if (evt.keyCode == ROTATE_CW_KEY && !evt.shift)
                    {
                        // R key: rotate clockwise by 90 degrees
                        currentYRotation += ROTATION_STEP;
                        if (currentYRotation >= 360f)
                            currentYRotation -= 360f;
                        evt.Use();
                        sceneView.Repaint();
                    }
                    else if (evt.keyCode == ROTATE_CCW_KEY || (evt.keyCode == ROTATE_CW_KEY && evt.shift))
                    {
                        // Shift+R or T: rotate counter-clockwise by 90 degrees
                        currentYRotation -= ROTATION_STEP;
                        if (currentYRotation < 0f)
                            currentYRotation += 360f;
                        evt.Use();
                        sceneView.Repaint();
                    }
                    else if (evt.keyCode == KeyCode.Escape)
                    {
                        // Escape: deactivate placement mode
                        Deactivate();
                        evt.Use();
                        sceneView.Repaint();
                    }
                    break;

                case EventType.ScrollWheel:
                    // Mouse scroll: adjust placement height
                    float scrollDelta = evt.delta.y > 0 ? -gridSnap.gridUnit : gridSnap.gridUnit;
                    currentYLevel += scrollDelta;
                    evt.Use();
                    sceneView.Repaint();
                    break;
            }
        }

        /// <summary>
        /// Updates ghost position based on mouse cursor position in the Scene View.
        /// Raycasts against colliders and grid plane to find placement position.
        /// </summary>
        private void UpdateGhostPositionFromMouse(Event evt)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);

            if (RaycastPlacement(ray, out Vector3 hitPoint))
            {
                // Snap position to grid
                ghostPosition = gridSnap.SnapWithOffset(hitPoint, currentBlock.SnapOffset);
                ghostPosition.y = currentYLevel;
            }
        }

        /// <summary>
        /// Raycasts from the given ray to find a valid placement position.
        /// Tries colliders first, then falls back to XZ plane at current Y level.
        /// </summary>
        private bool RaycastPlacement(Ray ray, out Vector3 hitPoint)
        {
            hitPoint = Vector3.zero;

            // Try raycasting against existing colliders in the scene
            if (Physics.Raycast(ray, out RaycastHit hit, 10000f, RAYCAST_LAYER_MASK))
            {
                hitPoint = hit.point;
                return true;
            }

            // Fallback: intersect with XZ plane at current Y level
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, currentYLevel, 0));
            if (groundPlane.Raycast(ray, out float enter) && enter > 0)
            {
                hitPoint = ray.origin + ray.direction * enter;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Updates the ghost preview's transform to match current placement state.
        /// </summary>
        private void UpdateGhostPreview(SceneView sceneView)
        {
            if (ghostInstance == null)
                return;

            ghostInstance.transform.position = ghostPosition;
            ghostInstance.transform.rotation = Quaternion.Euler(0, currentYRotation, 0);

            // Check if position is valid (optional: add collision detection here)
            UpdateGhostMaterialTransparency(ghostInstance, GHOST_TRANSPARENCY);
        }

        /// <summary>
        /// Draws visual feedback elements in the Scene View.
        /// Includes snap point highlight and HUD info.
        /// </summary>
        private void DrawVisualFeedback()
        {
            if (gridSnap == null)
                return;

            // Draw snap point indicator
            gridSnap.DrawSnapPoint(ghostPosition);

            // Draw HUD overlay
            DrawPlacementHUD();
        }

        /// <summary>
        /// Draws the placement mode HUD showing current state and keyboard shortcuts.
        /// </summary>
        private void DrawPlacementHUD()
        {
            Handles.BeginGUI();

            GUILayout.BeginArea(new Rect(10, 10, 300, 150));

            GUI.backgroundColor = new Color(0, 0, 0, 0.7f);
            GUI.Box(new Rect(0, 0, 300, 150), "");
            GUI.backgroundColor = Color.white;

            GUILayout.Label($"<b>Block Placement</b>", GetLabelStyle());
            GUILayout.Label($"Block: {currentBlock.DisplayName}", GetLabelStyle());
            GUILayout.Label($"Position: {ghostPosition:F1}", GetLabelStyle());
            GUILayout.Label($"Rotation: {currentYRotation}°", GetLabelStyle());
            GUILayout.Label($"Height: {currentYLevel:F1}", GetLabelStyle());
            GUILayout.Space(5);
            GUILayout.Label($"<size=10>[R] Rotate [Scroll] Height [ESC] Cancel</size>", GetLabelStyle());

            GUILayout.EndArea();

            Handles.EndGUI();
        }

        /// <summary>
        /// Creates a semi-transparent unlit material for the ghost preview.
        /// </summary>
        private Material CreateGhostMaterial()
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.SetFloat("_Mode", 3); // Transparent mode
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_BLEND");
            mat.renderQueue = 3000;

            Color ghostColor = settings != null ?
                settings.ghostColor :
                new Color(0.3f, 0.8f, 1f, GHOST_TRANSPARENCY);

            mat.color = ghostColor;

            return mat;
        }

        /// <summary>
        /// Applies the ghost material to all renderers in a GameObject and its children.
        /// </summary>
        private void ApplyGhostMaterialToRenderers(GameObject obj)
        {
            if (ghostMaterial == null)
                return;

            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                Material[] newMaterials = new Material[renderer.materials.Length];
                for (int i = 0; i < newMaterials.Length; i++)
                {
                    newMaterials[i] = ghostMaterial;
                }
                renderer.materials = newMaterials;
            }
        }

        /// <summary>
        /// Updates the transparency of all materials in a GameObject to provide visual feedback.
        /// </summary>
        private void UpdateGhostMaterialTransparency(GameObject obj, float alpha)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                foreach (Material mat in renderer.materials)
                {
                    Color col = mat.color;
                    col.a = alpha;
                    mat.color = col;
                }
            }
        }

        /// <summary>
        /// Places a block at the current ghost position in the scene.
        /// Handles instantiation, component setup, undo registration, and MapRoot registration.
        /// </summary>
        private void PlaceBlock()
        {
            if (currentBlock == null || currentBlock.Prefab == null)
                return;

            // Find or create MapRoot in the scene
            MapRoot mapRoot = Object.FindObjectOfType<MapRoot>();
            if (mapRoot == null)
            {
                GameObject mapRootObj = new GameObject("MapRoot");
                mapRoot = mapRootObj.AddComponent<MapRoot>();
                Undo.RegisterCreatedObjectUndo(mapRootObj, "Create MapRoot");
            }

            // Instantiate the block prefab
            GameObject blockInstance = Object.Instantiate(
                currentBlock.Prefab,
                ghostPosition,
                Quaternion.Euler(0, currentYRotation, 0),
                mapRoot.transform
            );

            // Setup undo support
            Undo.RegisterCreatedObjectUndo(blockInstance, $"Place {currentBlock.DisplayName}");

            // Add or get MapBlock component
            MapBlock mapBlock = blockInstance.GetComponent<MapBlock>();
            if (mapBlock == null)
            {
                mapBlock = blockInstance.AddComponent<MapBlock>();
            }

            // Configure the block
            mapBlock.SetBlockId(currentBlock.BlockId);
            mapBlock.definition = currentBlock;

            // Register with MapRoot
            mapRoot.RegisterBlock(mapBlock);

            // Name the GameObject
            blockInstance.name = $"{currentBlock.BlockId}_{mapRoot.BlockCount - 1}";

            // Mark scene as dirty
            EditorSceneManager.MarkSceneDirty(mapRoot.gameObject.scene);
        }

        /// <summary>
        /// Helper to get GUI label style with proper formatting.
        /// </summary>
        private GUIStyle GetLabelStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                fontSize = 11,
                normal = { textColor = Color.white }
            };
            return style;
        }
    }
}
