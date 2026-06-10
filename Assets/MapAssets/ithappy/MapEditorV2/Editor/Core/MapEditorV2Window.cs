using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using ithappy.MapEditor;

namespace ithappy.MapEditor.Editor
{
    /// <summary>
    /// Main EditorWindow for Map Editor V2.
    /// Ties together all subsystems: EditorState, SceneInteraction, GhostPreview, SurfaceSnap.
    /// Provides the block browser UI, controls, and SceneView rendering.
    /// </summary>
    public class MapEditorV2Window : EditorWindow
    {
        // Singleton
        private static MapEditorV2Window _instance;
        public static MapEditorV2Window Instance => _instance;

        // Subsystems
        private EditorState editorState;
        private SceneInteraction sceneInteraction;
        private GhostPreview ghostPreview;
        private SurfaceSnap surfaceSnap;

        // References
        private BlockDatabase database;
        private MapRoot currentMap;

        // UI state - cached, NOT rebuilt during OnGUI
        private List<BlockDefinition> cachedBlocks = new List<BlockDefinition>();
        private bool blockListDirty = true;
        private BlockCategory? selectedCategory;
        private string searchQuery = "";
        private Vector2 paletteScrollPos;
        private Vector2 categoryScrollPos;

        // Thumbnail preview cache (AssetPreview is async, cache results)
        private Dictionary<string, Texture> thumbnailCache = new Dictionary<string, Texture>();

        // Constants
        private const float SIDEBAR_WIDTH = 130f;
        private const float THUMBNAIL_SIZE = 100f;

        // Settings (reusing V1 MapEditorSettings)
        private MapEditorSettings mapEditorSettings;

        [MenuItem("Window/Map Editor V2 %&m")]
        public static void ShowWindow()
        {
            GetWindow<MapEditorV2Window>("Map Editor V2");
        }

        private void OnEnable()
        {
            _instance = this;

            // Create subsystems
            editorState = new EditorState();
            sceneInteraction = new SceneInteraction(editorState);
            ghostPreview = new GhostPreview();
            surfaceSnap = new SurfaceSnap();

            // Wire up connections
            sceneInteraction.SetSurfaceSnap(surfaceSnap);
            sceneInteraction.SetGhostPreview(ghostPreview);
            sceneInteraction.OnPlaceBlock = PlaceBlock;

            // Load database
            LoadDatabase();

            // Find or create MapRoot in scene
            currentMap = FindAnyObjectByType<MapRoot>(FindObjectsInactive.Include);
            if (currentMap == null)
            {
                GameObject mapGO = new GameObject("MapRoot");
                currentMap = mapGO.AddComponent<MapRoot>();
            }

            // Subscribe to SceneView events
            SceneView.duringSceneGui += OnSceneGUI;

            // Subscribe to editor state changes
            editorState.OnModeChanged += OnEditorModeChanged;

            blockListDirty = true;
            selectedCategory = null;
        }

        private void OnDisable()
        {
            // Unsubscribe from events
            SceneView.duringSceneGui -= OnSceneGUI;
            if (editorState != null)
            {
                editorState.OnModeChanged -= OnEditorModeChanged;
            }

            // Clean up ghost preview
            if (ghostPreview != null)
            {
                ghostPreview.Deactivate();
            }

            _instance = null;
        }

        private void LoadDatabase()
        {
            // Load V1 MapEditorSettings and get BlockDatabase from it
            mapEditorSettings = MapEditorSettings.GetOrCreateSettings();
            if (mapEditorSettings != null && mapEditorSettings.blockDatabase != null)
            {
                database = mapEditorSettings.blockDatabase;
            }
            else
            {
                Debug.LogWarning("[MapEditorV2] BlockDatabase not found in MapEditorSettings. Please assign it.");
                database = null;
            }
        }

        // Cached column count — computed once during Layout, used in Repaint
        private int cachedCols = 3;

        private void OnGUI()
        {
            // Rebuild cache BEFORE any drawing — must be identical across Layout/Repaint
            if (blockListDirty)
            {
                RebuildBlockCache();
                blockListDirty = false;
                previewsFullyLoaded = false;  // new blocks need preview loading
            }

            // Cache cols during Layout pass only (same value used in Repaint)
            if (Event.current.type == EventType.Layout)
            {
                cachedCols = Mathf.Max(1, (int)((position.width - SIDEBAR_WIDTH - 30f) / (THUMBNAIL_SIZE + 4f)));
            }

            // Top toolbar
            DrawToolbar();

            // Main content area with sidebar and palette
            GUILayout.BeginHorizontal();
            {
                // Left sidebar: categories
                DrawCategorySidebar();

                // Right area: search + palette
                GUILayout.BeginVertical();
                {
                    DrawSearchBar();
                    DrawBlockPalette();
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();

            // Bottom controls
            DrawControlsPanel();

            // Slowly refresh previews that haven't loaded yet (no flicker)
            SchedulePreviewRefresh();
        }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                EditorGUI.BeginChangeCheck();
                MapEditorSettings newSettings = (MapEditorSettings)EditorGUILayout.ObjectField(
                    mapEditorSettings,
                    typeof(MapEditorSettings),
                    false,
                    GUILayout.Width(200)
                );
                if (EditorGUI.EndChangeCheck() && newSettings != mapEditorSettings)
                {
                    mapEditorSettings = newSettings;
                    database = mapEditorSettings != null ? mapEditorSettings.blockDatabase : null;
                    blockListDirty = true;
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Find MapRoot", EditorStyles.toolbarButton, GUILayout.Width(100)))
                {
                    FindMapRootInScene();
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawCategorySidebar()
        {
            GUILayout.BeginVertical(GUILayout.Width(SIDEBAR_WIDTH));
            {
                GUILayout.Label("Categories", EditorStyles.boldLabel);
                categoryScrollPos = GUILayout.BeginScrollView(categoryScrollPos, GUILayout.Width(SIDEBAR_WIDTH));
                {
                    // "All" button
                    int allCount = database != null ? database.Count : 0;
                    DrawCategoryButton("All (" + allCount + ")", selectedCategory == null, () =>
                    {
                        selectedCategory = null;
                        blockListDirty = true;
                    });

                    GUILayout.Space(4);

                    // Category buttons — always show all 7 categories with counts
                    foreach (BlockCategory cat in System.Enum.GetValues(typeof(BlockCategory)))
                    {
                        int count = database != null ? database.GetByCategory(cat).Count : 0;
                        string label = cat.ToString() + " (" + count + ")";
                        DrawCategoryButton(label, selectedCategory == cat, () =>
                        {
                            selectedCategory = cat;
                            blockListDirty = true;
                        });
                    }
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndVertical();
        }

        private void DrawSearchBar()
        {
            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Search:", GUILayout.Width(60));
                string newQuery = GUILayout.TextField(searchQuery, GUILayout.ExpandWidth(true));
                if (newQuery != searchQuery)
                {
                    searchQuery = newQuery;
                    blockListDirty = true;
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawBlockPalette()
        {
            // ALWAYS draw scroll view regardless of block count
            paletteScrollPos = GUILayout.BeginScrollView(paletteScrollPos, GUILayout.ExpandHeight(true));

            if (cachedBlocks.Count == 0)
            {
                GUILayout.Label("No blocks found", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                // Use cachedCols (computed once during Layout, same in Repaint)
                for (int i = 0; i < cachedBlocks.Count; i += cachedCols)
                {
                    GUILayout.BeginHorizontal();
                    for (int j = 0; j < cachedCols; j++)
                    {
                        int idx = i + j;
                        if (idx < cachedBlocks.Count)
                        {
                            DrawBlockThumbnail(cachedBlocks[idx]);
                        }
                        else
                        {
                            // Empty spacer to keep layout consistent
                            GUILayout.Space(THUMBNAIL_SIZE + 4f);
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();
        }

        private void DrawBlockThumbnail(BlockDefinition block)
        {
            // NEVER early-return — must always draw the same number of controls
            string name = block != null ? block.DisplayName : "";
            bool isSelected = block != null && editorState != null
                && editorState.CurrentMode == EditorMode.Placing
                && editorState.SelectedPrefab == block;

            // Get or request preview texture
            Texture preview = GetBlockPreview(block);

            GUILayout.BeginVertical(GUILayout.Width(THUMBNAIL_SIZE + 6f));

            // Highlight selected block
            Color prevBg = GUI.backgroundColor;
            if (isSelected)
                GUI.backgroundColor = new Color(0.3f, 0.8f, 1f, 1f);

            if (GUILayout.Button(preview, GUILayout.Width(THUMBNAIL_SIZE), GUILayout.Height(THUMBNAIL_SIZE)))
            {
                if (block != null)
                {
                    editorState.EnterPlacingMode(block);
                    Repaint();
                    SceneView.RepaintAll();
                }
            }

            GUI.backgroundColor = prevBg;

            // Block name (truncated if too long)
            GUILayout.Label(name, EditorStyles.centeredGreyMiniLabel,
                GUILayout.Width(THUMBNAIL_SIZE), GUILayout.Height(14));

            GUILayout.EndVertical();
        }

        // Preview loading state
        private bool previewsFullyLoaded = false;
        private double nextPreviewCheckTime = 0;

        /// <summary>
        /// Gets the preview texture for a block. Caches results to avoid flicker.
        /// Never calls Repaint() directly — uses a delayed timer instead.
        /// </summary>
        private Texture GetBlockPreview(BlockDefinition block)
        {
            if (block == null || block.Prefab == null)
                return EditorGUIUtility.whiteTexture;

            string key = block.BlockId;

            // Return cached preview if available
            if (thumbnailCache.TryGetValue(key, out Texture cached) && cached != null)
                return cached;

            // Try to get full preview
            Texture2D preview = AssetPreview.GetAssetPreview(block.Prefab);
            if (preview != null)
            {
                // Make a persistent copy (AssetPreview textures can be destroyed)
                Texture2D copy = new Texture2D(preview.width, preview.height, preview.format, false);
                UnityEngine.Graphics.CopyTexture(preview, copy);
                thumbnailCache[key] = copy;
                return copy;
            }

            // Use mini thumbnail as stable fallback (won't flicker)
            Texture2D mini = AssetPreview.GetMiniThumbnail(block.Prefab);
            if (mini != null)
            {
                previewsFullyLoaded = false;  // flag that we still need full previews
                return mini;
            }

            previewsFullyLoaded = false;
            return EditorGUIUtility.whiteTexture;
        }

        /// <summary>
        /// Called from OnGUI to schedule a single delayed repaint for loading previews.
        /// Prevents the infinite Repaint loop that causes flickering.
        /// </summary>
        private void SchedulePreviewRefresh()
        {
            if (previewsFullyLoaded) return;

            double now = EditorApplication.timeSinceStartup;
            if (now < nextPreviewCheckTime) return;

            // Check again in 0.5 seconds
            nextPreviewCheckTime = now + 0.5;

            // Check if any previews are still loading
            bool anyLoading = false;
            foreach (BlockDefinition block in cachedBlocks)
            {
                if (block == null || block.Prefab == null) continue;
                if (!thumbnailCache.ContainsKey(block.BlockId))
                {
                    Texture2D preview = AssetPreview.GetAssetPreview(block.Prefab);
                    if (preview != null)
                    {
                        Texture2D copy = new Texture2D(preview.width, preview.height, preview.format, false);
                        UnityEngine.Graphics.CopyTexture(preview, copy);
                        thumbnailCache[block.BlockId] = copy;
                    }
                    else
                    {
                        anyLoading = true;
                    }
                }
            }

            if (anyLoading)
            {
                // Schedule ONE repaint, not continuous
                EditorApplication.delayCall += () => Repaint();
            }
            else
            {
                previewsFullyLoaded = true;
            }
        }

        /// <summary>
        /// Draws a styled category button with selection highlight.
        /// </summary>
        private void DrawCategoryButton(string label, bool isSelected, System.Action onClick)
        {
            Color prevBg = GUI.backgroundColor;
            if (isSelected)
                GUI.backgroundColor = new Color(0.3f, 0.8f, 1f, 1f);

            GUIStyle style = isSelected ? EditorStyles.toolbarButton : EditorStyles.miniButton;
            if (GUILayout.Button(label, style, GUILayout.Height(22)))
            {
                onClick?.Invoke();
            }

            GUI.backgroundColor = prevBg;
        }

        private void DrawControlsPanel()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            {
                // Grid and snap controls
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Grid:", GUILayout.Width(50));
                    surfaceSnap.gridUnit = EditorGUILayout.FloatField(surfaceSnap.gridUnit, GUILayout.Width(60));

                    GUILayout.Label("Snap:", GUILayout.Width(40));
                    surfaceSnap.gridSnapEnabled = EditorGUILayout.Toggle(
                        surfaceSnap.gridSnapEnabled,
                        GUILayout.Width(20)
                    );

                    GUILayout.Label("Surface:", GUILayout.Width(60));
                    surfaceSnap.surfaceSnapEnabled = EditorGUILayout.Toggle(
                        surfaceSnap.surfaceSnapEnabled,
                        GUILayout.Width(20)
                    );

                    GUILayout.Label("Edge:", GUILayout.Width(40));
                    surfaceSnap.edgeSnapEnabled = EditorGUILayout.Toggle(
                        surfaceSnap.edgeSnapEnabled,
                        GUILayout.Width(20)
                    );

                    GUILayout.Label("Y:", GUILayout.Width(20));
                    surfaceSnap.currentYLevel = EditorGUILayout.FloatField(
                        surfaceSnap.currentYLevel,
                        GUILayout.Width(60)
                    );
                }
                GUILayout.EndHorizontal();

                // Map info
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label($"Map: {(currentMap != null ? currentMap.MapName : "None")}", GUILayout.Width(200));
                    GUILayout.Label($"Blocks: {(currentMap != null ? currentMap.BlockCount : 0)}", GUILayout.Width(150));
                }
                GUILayout.EndHorizontal();

                // Action buttons
                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Save", GUILayout.Width(80)))
                    {
                        SaveMap();
                    }

                    if (GUILayout.Button("Load", GUILayout.Width(80)))
                    {
                        LoadMap();
                    }

                    if (GUILayout.Button("Clear", GUILayout.Width(80)))
                    {
                        ClearMap();
                    }
                }
                GUILayout.EndHorizontal();

                // Selection info
                if (editorState.CurrentMode == EditorMode.Placing && editorState.SelectedPrefab != null)
                {
                    GUILayout.Label(
                        $"Selected: {editorState.SelectedPrefab.DisplayName}",
                        EditorStyles.boldLabel
                    );
                }
            }
            GUILayout.EndVertical();
        }

        private void RebuildBlockCache()
        {
            cachedBlocks.Clear();

            if (database == null)
                return;

            // Filter blocks based on category and search
            List<BlockDefinition> candidates;

            if (selectedCategory.HasValue)
            {
                candidates = database.GetByCategory(selectedCategory.Value);
            }
            else
            {
                candidates = database.GetAllBlocks();
            }

            // Apply search filter
            if (!string.IsNullOrEmpty(searchQuery))
            {
                var searchResults = database.Search(searchQuery);
                candidates = new List<BlockDefinition>();
                foreach (var result in searchResults)
                {
                    if (selectedCategory.HasValue && result.Category != selectedCategory.Value)
                        continue;
                    candidates.Add(result);
                }
            }

            cachedBlocks = candidates;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            // Route input to SceneInteraction
            sceneInteraction.OnSceneGUI(sceneView);

            // Update ghost preview position
            if (ghostPreview.IsActive && editorState.CurrentMode == EditorMode.Placing)
            {
                ghostPreview.UpdateTransform(
                    sceneInteraction.CurrentSnappedPosition,
                    Quaternion.Euler(0, editorState.CurrentYRotation, 0)
                );
            }

            // Render visuals only during Repaint
            if (Event.current.type == EventType.Repaint)
            {
                // Draw grid
                if (surfaceSnap.gridSnapEnabled)
                {
                    surfaceSnap.DrawGrid(sceneView);
                }

                // Draw snap indicator and edge guides in placing mode
                if (editorState.CurrentMode == EditorMode.Placing && editorState.SelectedPrefab != null)
                {
                    surfaceSnap.DrawSnapIndicator(sceneInteraction.CurrentSnappedPosition);
                    Bounds placingBounds = editorState.SelectedPrefab.GetLocalBounds();
                    surfaceSnap.DrawEdgeSnapGuides(sceneInteraction.CurrentSnappedPosition, placingBounds);
                    surfaceSnap.DrawCenterSnapGuides(sceneInteraction.CurrentSnappedPosition, placingBounds);
                }

                // Draw selection highlights
                DrawSelectionHighlights();

                // Draw scene HUD
                DrawSceneHUD(sceneView);
            }
        }

        private void DrawSelectionHighlights()
        {
            if (editorState.SelectedBlocks.Count == 0)
                return;

            Handles.color = new Color(1f, 0.8f, 0f, 1f);

            foreach (MapBlock block in editorState.SelectedBlocks)
            {
                if (block == null)
                    continue;

                Bounds bounds = block.GetWorldBounds();
                Handles.DrawWireCube(bounds.center, bounds.size);
            }

            Handles.color = Color.white;
        }

        private void DrawSceneHUD(SceneView sceneView)
        {
            Handles.BeginGUI();
            {
                // Display current mode and info
                string modeText = editorState.CurrentMode.ToString();
                if (editorState.CurrentMode == EditorMode.Placing && editorState.SelectedPrefab != null)
                {
                    modeText += $": {editorState.SelectedPrefab.DisplayName}";
                }

                Rect hudRect = new Rect(10, 10, 300, 100);
                GUI.Box(hudRect, "");
                GUI.Label(new Rect(20, 15, 280, 20), $"Mode: {modeText}", EditorStyles.label);
                GUI.Label(new Rect(20, 35, 280, 20), $"Y Rotation: {editorState.CurrentYRotation}°", EditorStyles.label);
                GUI.Label(new Rect(20, 55, 280, 20), $"Y Level: {surfaceSnap.currentYLevel:F2}", EditorStyles.label);
                GUI.Label(new Rect(20, 75, 280, 20), $"Shortcuts: R/T(rotate), ESC(cancel)", EditorStyles.miniLabel);
            }
            Handles.EndGUI();
        }

        private GameObject PlaceBlock(Vector3 position, Quaternion rotation, BlockDefinition block)
        {
            if (currentMap == null || block == null || block.Prefab == null)
                return null;

            GameObject instance = Instantiate(block.Prefab, position, rotation, currentMap.transform);
            instance.name = $"{block.BlockId}_{currentMap.BlockCount}";

            MapBlock mapBlock = instance.GetComponent<MapBlock>();
            if (mapBlock == null)
                mapBlock = instance.AddComponent<MapBlock>();

            mapBlock.SetBlockId(block.BlockId);
            currentMap.RegisterBlock(mapBlock);

            Undo.RegisterCreatedObjectUndo(instance, $"Place {block.DisplayName}");

            return instance;
        }

        private void OnEditorModeChanged(EditorMode oldMode, EditorMode newMode)
        {
            if (newMode == EditorMode.Placing && editorState.SelectedPrefab != null)
            {
                ghostPreview.Activate(editorState.SelectedPrefab);
            }
            else if (oldMode == EditorMode.Placing)
            {
                ghostPreview.Deactivate();
            }

            Repaint();
            SceneView.RepaintAll();
        }

        private void FindMapRootInScene()
        {
            MapRoot map = FindAnyObjectByType<MapRoot>(FindObjectsInactive.Include);
            if (map != null)
            {
                currentMap = map;
                EditorGUIUtility.PingObject(map.gameObject);
                Debug.Log($"[MapEditorV2Window] Found MapRoot: {map.gameObject.name}");
            }
            else
            {
                Debug.LogWarning("[MapEditorV2Window] No MapRoot found in scene");
            }
        }

        private void SaveMap()
        {
            if (currentMap == null)
            {
                EditorUtility.DisplayDialog("Error", "No MapRoot selected", "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanel("Save Map", "Assets/", currentMap.MapName, "json");
            if (string.IsNullOrEmpty(path))
                return;

            MapSerializer.SaveToJson(currentMap, path);
            Debug.Log($"[MapEditorV2Window] Map saved to {path}");
        }

        private void LoadMap()
        {
            string path = EditorUtility.OpenFilePanel("Load Map", "Assets/", "json");
            if (string.IsNullOrEmpty(path))
                return;

            MapData mapData = MapSerializer.LoadFromJson(path);
            if (mapData == null)
            {
                EditorUtility.DisplayDialog("Error", "Failed to load map", "OK");
                return;
            }

            if (currentMap != null && database != null)
            {
                currentMap.ImportFromMapData(mapData, database);
                Debug.Log($"[MapEditorV2Window] Map loaded from {path}");
            }
        }

        private void ClearMap()
        {
            if (currentMap == null)
                return;

            if (EditorUtility.DisplayDialog("Clear Map", "Remove all blocks?", "Yes", "No"))
            {
                currentMap.ClearAllBlocks();
                Debug.Log("[MapEditorV2Window] Map cleared");
            }
        }
    }
}
