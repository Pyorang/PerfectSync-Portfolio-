using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using ithappy.MapEditor;

namespace ithappy.MapEditor.Editor
{
    /// <summary>
    /// Main editor window for the Map Editor system.
    /// Open via menu "Window/Map Editor" or Ctrl+Alt+M.
    /// </summary>
    public class MapEditorWindow : EditorWindow
    {
        private static MapEditorWindow _instance;
        public static MapEditorWindow Instance => _instance;

        // References
        private MapEditorSettings settings;
        private BlockDatabase database;
        private MapRoot currentMap;
        private GridSnapSystem gridSnap;

        // State
        private BlockDefinition selectedBlockDefinition;
        private BlockCategory? selectedCategory;
        private string searchQuery = "";

        // UI State
        private Vector2 paletteScrollPos;
        private Vector2 categoryScrollPos;

        // Cached block list — rebuilt only when input changes, NOT during OnGUI
        private List<BlockDefinition> cachedBlocks = new List<BlockDefinition>();
        private bool blockListDirty = true;

        // Placement state
        private float currentYRotation = 0f;

        // Constants
        private const float SIDEBAR_WIDTH = 120f;
        private const float THUMBNAIL_SIZE = 80f;
        private const int COLUMNS = 3;

        // Events
        public static event System.Action<BlockDefinition> OnBlockSelected;
        public static event System.Action OnBlockDeselected;

        // Static accessors
        public static BlockDefinition SelectedBlock => Instance?.selectedBlockDefinition;
        public static GridSnapSystem GridSnap => Instance?.gridSnap;
        public static MapRoot CurrentMap => Instance?.currentMap;
        public static bool IsPlacementMode => Instance?.selectedBlockDefinition != null;

        [MenuItem("Window/Map Editor %&m")]
        public static void ShowWindow()
        {
            GetWindow<MapEditorWindow>("Map Editor");
        }

        private void OnEnable()
        {
            _instance = this;
            gridSnap = new GridSnapSystem();

            settings = MapEditorSettings.GetOrCreateSettings();
            if (settings != null)
            {
                database = settings.blockDatabase;
                gridSnap.gridUnit = settings.defaultGridSize;
                gridSnap.snapEnabled = settings.snapByDefault;
                gridSnap.showGrid = settings.showGridByDefault;
                gridSnap.rotationSnapAngle = settings.rotationSnapAngle;
                gridSnap.gridExtent = settings.gridExtent;
            }

            currentMap = FindObjectOfType<MapRoot>();
            blockListDirty = true;

            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            _instance = null;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                DeselectBlock();
        }

        /// <summary>
        /// Rebuilds the cached block list. Called outside of OnGUI to prevent
        /// layout mismatches between Layout and Repaint passes.
        /// </summary>
        private void RebuildBlockList()
        {
            cachedBlocks.Clear();
            if (database == null) return;

            if (!string.IsNullOrEmpty(searchQuery))
            {
                cachedBlocks = database.Search(searchQuery);
                if (selectedCategory.HasValue)
                    cachedBlocks.RemoveAll(b => b == null || b.Category != selectedCategory.Value);
            }
            else if (selectedCategory.HasValue)
            {
                cachedBlocks = database.GetByCategory(selectedCategory.Value);
            }
            else
            {
                cachedBlocks = database.GetAllBlocks();
            }

            // Remove nulls once
            cachedBlocks.RemoveAll(b => b == null);
            blockListDirty = false;
        }

        // =====================================================================
        // OnGUI — ALL Begin/End calls are UNCONDITIONAL. No layout inside if/else.
        // =====================================================================
        private void OnGUI()
        {
            // Rebuild block list once per logical change, NOT during layout
            if (blockListDirty)
                RebuildBlockList();

            // --- Toolbar ---
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            DrawToolbarContent();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5f);

            // --- Main area: sidebar + content ---
            EditorGUILayout.BeginHorizontal();

            // Left sidebar
            EditorGUILayout.BeginVertical(GUILayout.Width(SIDEBAR_WIDTH));
            DrawCategoryContent();
            EditorGUILayout.EndVertical();

            // Right content
            EditorGUILayout.BeginVertical();
            DrawPaletteContent();
            EditorGUILayout.Space(5f);
            DrawControlsContent();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        // =====================================================================
        // Toolbar
        // =====================================================================
        private void DrawToolbarContent()
        {
            EditorGUI.BeginChangeCheck();
            MapEditorSettings newSettings = EditorGUILayout.ObjectField(
                settings, typeof(MapEditorSettings), false, GUILayout.Width(150f)
            ) as MapEditorSettings;
            if (EditorGUI.EndChangeCheck() && newSettings != settings)
            {
                settings = newSettings;
                database = settings != null ? settings.blockDatabase : null;
                blockListDirty = true;
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Find MapRoot", EditorStyles.toolbarButton, GUILayout.Width(100f)))
            {
                currentMap = FindObjectOfType<MapRoot>();
                if (currentMap == null)
                {
                    var go = new GameObject("MapRoot");
                    currentMap = go.AddComponent<MapRoot>();
                    Undo.RegisterCreatedObjectUndo(go, "Create MapRoot");
                }
            }
        }

        // =====================================================================
        // Category sidebar
        // =====================================================================
        private void DrawCategoryContent()
        {
            GUILayout.Label("Categories", EditorStyles.boldLabel);

            categoryScrollPos = GUILayout.BeginScrollView(categoryScrollPos, GUILayout.Width(SIDEBAR_WIDTH));

            // "All" button
            GUI.color = selectedCategory == null ? Color.cyan : Color.white;
            if (GUILayout.Button("All", EditorStyles.miniButton))
            {
                selectedCategory = null;
                blockListDirty = true;
                paletteScrollPos = Vector2.zero;
            }

            // Category buttons
            foreach (BlockCategory cat in System.Enum.GetValues(typeof(BlockCategory)))
            {
                GUI.color = selectedCategory == cat ? Color.cyan : Color.white;
                if (GUILayout.Button(cat.ToString(), EditorStyles.miniButton))
                {
                    selectedCategory = cat;
                    blockListDirty = true;
                    paletteScrollPos = Vector2.zero;
                }
            }
            GUI.color = Color.white;

            GUILayout.EndScrollView();
        }

        // =====================================================================
        // Block palette — ALWAYS draws ScrollView regardless of block count
        // =====================================================================
        private void DrawPaletteContent()
        {
            GUILayout.Label("Blocks", EditorStyles.boldLabel);

            // Search bar
            EditorGUI.BeginChangeCheck();
            string newSearch = EditorGUILayout.TextField("Search", searchQuery);
            if (EditorGUI.EndChangeCheck())
            {
                searchQuery = newSearch;
                blockListDirty = true;
                paletteScrollPos = Vector2.zero;
            }

            // ALWAYS draw scroll view — never skip it based on data
            paletteScrollPos = GUILayout.BeginScrollView(paletteScrollPos, GUILayout.MinHeight(150f));

            if (cachedBlocks.Count == 0)
            {
                GUILayout.Label("No blocks found.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                // Calculate columns based on window width
                float availableWidth = EditorGUIUtility.currentViewWidth - SIDEBAR_WIDTH - 30f;
                int cols = Mathf.Max(1, Mathf.FloorToInt(availableWidth / (THUMBNAIL_SIZE + 14f)));

                for (int i = 0; i < cachedBlocks.Count; i += cols)
                {
                    EditorGUILayout.BeginHorizontal();
                    for (int j = 0; j < cols && (i + j) < cachedBlocks.Count; j++)
                    {
                        DrawSingleBlock(cachedBlocks[i + j]);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();
        }

        /// <summary>
        /// Draws a single block thumbnail + name. Fully self-contained Begin/End.
        /// </summary>
        private void DrawSingleBlock(BlockDefinition block)
        {
            bool isSelected = (selectedBlockDefinition == block);

            EditorGUILayout.BeginVertical(GUILayout.Width(THUMBNAIL_SIZE + 10f));

            // Thumbnail
            Texture preview = block.Thumbnail;
            if (preview == null && block.Prefab != null)
                preview = AssetPreview.GetAssetPreview(block.Prefab);

            GUIStyle style = isSelected ? GUI.skin.button : EditorStyles.miniButton;
            if (isSelected) GUI.color = Color.cyan;

            if (GUILayout.Button(
                preview != null ? preview : Texture2D.grayTexture,
                style,
                GUILayout.Width(THUMBNAIL_SIZE),
                GUILayout.Height(THUMBNAIL_SIZE)))
            {
                SelectBlock(block);
            }

            GUI.color = Color.white;

            // Name label
            GUILayout.Label(block.DisplayName, EditorStyles.miniLabel, GUILayout.Width(THUMBNAIL_SIZE));

            EditorGUILayout.EndVertical();
        }

        // =====================================================================
        // Grid & Map controls
        // =====================================================================
        private void DrawControlsContent()
        {
            // Grid settings
            GUILayout.Label("Grid Settings", EditorStyles.boldLabel);
            gridSnap.gridUnit = EditorGUILayout.FloatField("Grid Size", gridSnap.gridUnit);
            gridSnap.snapEnabled = EditorGUILayout.Toggle("Snap", gridSnap.snapEnabled);
            gridSnap.showGrid = EditorGUILayout.Toggle("Show Grid", gridSnap.showGrid);
            gridSnap.rotationSnapAngle = EditorGUILayout.FloatField("Rotation Snap", gridSnap.rotationSnapAngle);
            gridSnap.currentYLevel = EditorGUILayout.FloatField("Y Level", gridSnap.currentYLevel);

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // Map info
            GUILayout.Label("Map", EditorStyles.boldLabel);

            string mapName = currentMap != null ? currentMap.MapName : "(no MapRoot)";
            string blockCount = currentMap != null ? currentMap.BlockCount.ToString() : "0";
            EditorGUILayout.LabelField("Name", mapName);
            EditorGUILayout.LabelField("Blocks", blockCount);

            EditorGUILayout.Space(3f);

            // Action buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save", GUILayout.Height(25f))) SaveMapToJSON();
            if (GUILayout.Button("Load", GUILayout.Height(25f))) LoadMapFromJSON();
            if (GUILayout.Button("Clear", GUILayout.Height(25f))) ClearAllBlocks();
            EditorGUILayout.EndHorizontal();

            // Selected block info
            if (selectedBlockDefinition != null)
            {
                EditorGUILayout.Space(3f);
                EditorGUILayout.HelpBox(
                    $"Selected: {selectedBlockDefinition.DisplayName}\nLeft-click Scene to place. Right-click to deselect.",
                    MessageType.Info);
            }
        }

        // =====================================================================
        // Block selection
        // =====================================================================
        private void SelectBlock(BlockDefinition block)
        {
            if (block == null) return;
            selectedBlockDefinition = block;
            currentYRotation = 0f;
            OnBlockSelected?.Invoke(block);
            SceneView.RepaintAll();
            Repaint();
        }

        private void DeselectBlock()
        {
            selectedBlockDefinition = null;
            OnBlockDeselected?.Invoke();
            SceneView.RepaintAll();
            Repaint();
        }

        // =====================================================================
        // Save / Load / Clear
        // =====================================================================
        private void SaveMapToJSON()
        {
            if (currentMap == null) { EditorUtility.DisplayDialog("Error", "No MapRoot in scene.", "OK"); return; }

            string path = EditorUtility.SaveFilePanel("Save Map",
                settings?.defaultSavePath ?? "Assets/Maps", currentMap.MapName + ".json", "json");
            if (string.IsNullOrEmpty(path)) return;

            MapSerializer.SaveToJson(currentMap, path);
            EditorUtility.DisplayDialog("Saved", path, "OK");
        }

        private void LoadMapFromJSON()
        {
            if (currentMap == null) { EditorUtility.DisplayDialog("Error", "No MapRoot in scene.", "OK"); return; }
            if (database == null) { EditorUtility.DisplayDialog("Error", "No BlockDatabase in settings.", "OK"); return; }

            string path = EditorUtility.OpenFilePanel("Load Map",
                settings?.defaultSavePath ?? "Assets/Maps", "json");
            if (string.IsNullOrEmpty(path)) return;

            MapData mapData = MapSerializer.LoadFromJson(path);
            if (mapData == null) { EditorUtility.DisplayDialog("Error", "Failed to load.", "OK"); return; }

            Undo.RecordObject(currentMap, "Load Map");
            currentMap.ImportFromMapData(mapData, database);
            EditorUtility.DisplayDialog("Loaded", $"{mapData.blocks.Count} blocks loaded.", "OK");
        }

        private void ClearAllBlocks()
        {
            if (currentMap == null) return;
            if (!EditorUtility.DisplayDialog("Clear All", "Delete all blocks?", "Yes", "Cancel")) return;

            Undo.RecordObject(currentMap, "Clear All Blocks");
            currentMap.ClearAllBlocks();
        }

        // =====================================================================
        // Scene View integration
        // =====================================================================
        private void OnSceneGUI(SceneView sceneView)
        {
            if (gridSnap == null) return;

            if (gridSnap.showGrid)
                gridSnap.DrawGrid(sceneView);

            if (!IsPlacementMode || currentMap == null) return;

            Event e = Event.current;

            // Consume default scene input so our clicks/keys aren't eaten by Unity
            if (e.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }

            // Calculate snapped position from mouse
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            float denom = ray.direction.y;
            if (Mathf.Abs(denom) < 0.0001f) denom = 0.0001f;
            float t = (gridSnap.currentYLevel - ray.origin.y) / denom;
            Vector3 hitPos = ray.origin + ray.direction * Mathf.Max(0, t);
            Vector3 snappedPos = gridSnap.SnapPositionXZ(hitPos);

            // --- Ghost preview (Repaint only, no GUILayout) ---
            if (e.type == EventType.Repaint)
            {
                Bounds bounds = selectedBlockDefinition.GetLocalBounds();
                gridSnap.DrawBlockGhostBounds(snappedPos, bounds.size);
                gridSnap.DrawSnapPoint(snappedPos);

                // HUD overlay — use GUI.Label (immediate mode), NOT GUILayout
                Handles.BeginGUI();
                Rect hudRect = new Rect(10, 10, 280, 70);
                GUI.Box(hudRect, "", EditorStyles.helpBox);
                GUI.Label(new Rect(16, 12, 270, 20), $"Block: {selectedBlockDefinition.DisplayName}", EditorStyles.boldLabel);
                GUI.Label(new Rect(16, 32, 270, 20), $"Rotation: {currentYRotation}\u00b0   Y Level: {gridSnap.currentYLevel:F1}");
                GUI.Label(new Rect(16, 50, 270, 20), "[R] Rotate  [Scroll] Height  [ESC] Cancel", EditorStyles.miniLabel);
                Handles.EndGUI();
            }

            // --- Left click → place block ---
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                PlaceBlock(snappedPos, Quaternion.Euler(0f, currentYRotation, 0f));
                e.Use();
            }

            // --- Right click → deselect ---
            if (e.type == EventType.MouseDown && e.button == 1)
            {
                DeselectBlock();
                e.Use();
            }

            // --- Keyboard input ---
            if (e.type == EventType.KeyDown)
            {
                switch (e.keyCode)
                {
                    case KeyCode.R:
                        if (e.shift)
                            currentYRotation -= 90f; // Shift+R: counter-clockwise
                        else
                            currentYRotation += 90f; // R: clockwise

                        // Normalize to 0~360
                        currentYRotation = ((currentYRotation % 360f) + 360f) % 360f;
                        e.Use();
                        sceneView.Repaint();
                        Repaint();
                        break;

                    case KeyCode.T:
                        // T: counter-clockwise
                        currentYRotation -= 90f;
                        currentYRotation = ((currentYRotation % 360f) + 360f) % 360f;
                        e.Use();
                        sceneView.Repaint();
                        Repaint();
                        break;

                    case KeyCode.Escape:
                        DeselectBlock();
                        currentYRotation = 0f;
                        e.Use();
                        sceneView.Repaint();
                        break;
                }
            }

            // --- Scroll wheel → adjust Y level ---
            if (e.type == EventType.ScrollWheel)
            {
                // Scroll up (negative delta.y) = increase height, scroll down = decrease
                float step = gridSnap.gridUnit;
                gridSnap.currentYLevel += e.delta.y > 0 ? -step : step;
                e.Use();
                sceneView.Repaint();
                Repaint(); // Update Y Level field in editor window
            }

            // --- Mouse move → repaint for ghost tracking ---
            if (e.type == EventType.MouseMove)
            {
                sceneView.Repaint();
            }
        }

        private void PlaceBlock(Vector3 position, Quaternion rotation)
        {
            if (selectedBlockDefinition == null || selectedBlockDefinition.Prefab == null) return;

            GameObject instance = Instantiate(
                selectedBlockDefinition.Prefab, position, rotation, currentMap.transform);
            instance.name = $"{selectedBlockDefinition.BlockId}_{currentMap.BlockCount}";

            MapBlock mapBlock = instance.GetComponent<MapBlock>();
            if (mapBlock == null) mapBlock = instance.AddComponent<MapBlock>();

            mapBlock.SetBlockId(selectedBlockDefinition.BlockId);
            currentMap.RegisterBlock(mapBlock);

            Undo.RegisterCreatedObjectUndo(instance, $"Place {selectedBlockDefinition.DisplayName}");
        }
    }
}
