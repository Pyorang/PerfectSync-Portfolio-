using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using ithappy.MapEditor;

namespace ithappy.MapEditor.Editor
{
    /// <summary>
    /// Helper class for rendering block palette UI components.
    /// Provides reusable methods for drawing block grids, thumbnails, and category filters.
    /// Used by MapEditorWindow to organize UI drawing logic.
    /// </summary>
    public static class BlockPaletteUI
    {
        /// <summary>
        /// Styles cached for performance.
        /// </summary>
        private static class Styles
        {
            public static readonly GUIStyle CategoryButtonActive;
            public static readonly GUIStyle CategoryButtonInactive;
            public static readonly GUIStyle BlockThumbnailSelected;
            public static readonly GUIStyle BlockThumbnailNormal;
            public static readonly GUIStyle BlockLabel;

            static Styles()
            {
                CategoryButtonActive = new GUIStyle(EditorStyles.miniButton)
                {
                    normal = { background = EditorGUIUtility.Load("builtin skins/darkskin/images/btn.png") as Texture2D }
                };

                CategoryButtonInactive = EditorStyles.miniButton;

                BlockThumbnailSelected = new GUIStyle(EditorStyles.toolbarButton);

                BlockThumbnailNormal = new GUIStyle(EditorStyles.miniButton);

                BlockLabel = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
            }
        }

        /// <summary>
        /// Draws a vertical category button list.
        /// Returns true if the selected category changed.
        /// </summary>
        /// <param name="selectedCategory">The currently selected category (null for "All")</param>
        /// <param name="width">Width of the button list</param>
        /// <param name="scrollPos">Scroll position reference</param>
        /// <returns>The newly selected category, or null if "All" was selected</returns>
        public static BlockCategory? DrawCategoryList(BlockCategory? selectedCategory, float width, ref Vector2 scrollPos)
        {
            BlockCategory? newSelection = selectedCategory;

            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Width(width));
            {
                // "All" button
                bool allSelected = selectedCategory == null;
                string allLabel = allSelected ? "▼ All" : "All";

                if (GUILayout.Button(allLabel, EditorStyles.miniButton))
                {
                    newSelection = null;
                }

                // Category buttons
                foreach (BlockCategory category in System.Enum.GetValues(typeof(BlockCategory)))
                {
                    bool isSelected = selectedCategory == category;
                    string label = isSelected ? "▼ " + category.ToString() : category.ToString();

                    if (GUILayout.Button(label, EditorStyles.miniButton))
                    {
                        newSelection = category;
                    }
                }
            }
            GUILayout.EndScrollView();

            return newSelection;
        }

        /// <summary>
        /// Draws a search bar and returns the new search query.
        /// </summary>
        /// <param name="currentQuery">Current search query</param>
        /// <returns>Updated search query</returns>
        public static string DrawSearchBar(string currentQuery)
        {
            string newQuery = EditorGUILayout.TextField("Search", currentQuery, GUILayout.Height(20f));
            return newQuery;
        }

        /// <summary>
        /// Draws a grid of block thumbnails with wrapping layout.
        /// </summary>
        /// <param name="blocks">List of blocks to display</param>
        /// <param name="selectedBlock">Currently selected block</param>
        /// <param name="scrollPos">Scroll position reference</param>
        /// <param name="thumbnailSize">Size of each thumbnail in pixels</param>
        /// <param name="columnsPerRow">Number of thumbnails per row</param>
        /// <returns>The newly selected block, or null if no selection changed</returns>
        public static BlockDefinition DrawBlockGrid(
            List<BlockDefinition> blocks,
            BlockDefinition selectedBlock,
            ref Vector2 scrollPos,
            float thumbnailSize = 80f,
            int columnsPerRow = 2)
        {
            BlockDefinition newSelection = selectedBlock;

            if (blocks == null || blocks.Count == 0)
            {
                EditorGUILayout.HelpBox("No blocks found.", MessageType.Info);
                return newSelection;
            }

            scrollPos = GUILayout.BeginScrollView(scrollPos);
            {
                EditorGUILayout.BeginHorizontal();
                int columnCount = 0;

                foreach (BlockDefinition block in blocks)
                {
                    if (block == null)
                        continue;

                    if (DrawBlockThumbnail(block, selectedBlock == block, thumbnailSize))
                    {
                        newSelection = block;
                    }

                    columnCount++;
                    if (columnCount >= columnsPerRow)
                    {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        columnCount = 0;
                    }
                }

                // Always close the last BeginHorizontal
                EditorGUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            return newSelection;
        }

        /// <summary>
        /// Draws a single block thumbnail button.
        /// </summary>
        /// <param name="block">The block to display</param>
        /// <param name="isSelected">Whether this block is currently selected</param>
        /// <param name="thumbnailSize">Size of the thumbnail</param>
        /// <returns>True if the button was clicked</returns>
        private static bool DrawBlockThumbnail(BlockDefinition block, bool isSelected, float thumbnailSize)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(thumbnailSize + 10f));
            {
                // Get preview texture
                Texture preview = null;
                if (block.Thumbnail != null)
                {
                    preview = block.Thumbnail;
                }
                else if (block.Prefab != null)
                {
                    preview = AssetPreview.GetAssetPreview(block.Prefab);
                }

                // Draw thumbnail button
                GUIStyle thumbStyle = isSelected ? EditorStyles.toolbarButton : EditorStyles.miniButton;

                Rect thumbRect = EditorGUILayout.GetControlRect(
                    GUILayout.Width(thumbnailSize),
                    GUILayout.Height(thumbnailSize)
                );

                bool clicked = GUI.Button(thumbRect, preview, thumbStyle);

                // Draw block name below thumbnail
                EditorGUILayout.LabelField(
                    block.DisplayName,
                    EditorStyles.miniLabel,
                    GUILayout.Width(thumbnailSize)
                );

                EditorGUILayout.EndVertical();

                return clicked;
            }
        }

        /// <summary>
        /// Draws grid and snap controls in a compact format.
        /// </summary>
        /// <param name="gridSnap">The grid snap system to configure</param>
        public static void DrawGridControls(GridSnapSystem gridSnap)
        {
            if (gridSnap == null)
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                GUILayout.Label("Grid Settings", EditorStyles.boldLabel);

                gridSnap.gridUnit = EditorGUILayout.FloatField("Grid Size", gridSnap.gridUnit);
                gridSnap.snapEnabled = EditorGUILayout.Toggle("Snap Enabled", gridSnap.snapEnabled);
                gridSnap.showGrid = EditorGUILayout.Toggle("Show Grid", gridSnap.showGrid);
                gridSnap.rotationSnapAngle = EditorGUILayout.FloatField("Rotation Snap", gridSnap.rotationSnapAngle);
                gridSnap.currentYLevel = EditorGUILayout.FloatField("Y Level", gridSnap.currentYLevel);
                gridSnap.gridExtent = EditorGUILayout.IntField("Grid Extent", gridSnap.gridExtent);
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws map control buttons and information.
        /// </summary>
        /// <param name="mapRoot">The map root to display info for</param>
        /// <returns>An action code: 0 = none, 1 = save, 2 = load, 3 = clear</returns>
        public static int DrawMapControls(MapRoot mapRoot)
        {
            if (mapRoot == null)
            {
                EditorGUILayout.HelpBox("No MapRoot found in scene.", MessageType.Warning);
                return 0;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                GUILayout.Label("Map Controls", EditorStyles.boldLabel);

                // Display map info
                EditorGUILayout.LabelField("Map Name", mapRoot.MapName);
                EditorGUILayout.LabelField("Block Count", mapRoot.BlockCount.ToString());

                GUILayout.Space(5f);

                // Buttons
                int action = 0;

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Save JSON", GUILayout.Height(25f)))
                    {
                        action = 1;
                    }

                    if (GUILayout.Button("Load JSON", GUILayout.Height(25f)))
                    {
                        action = 2;
                    }
                }
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Clear All", GUILayout.Height(25f)))
                {
                    action = 3;
                }

                EditorGUILayout.EndVertical();

                return action;
            }
        }

        /// <summary>
        /// Draws a category filter chip with an optional close button.
        /// </summary>
        /// <param name="category">The category to display</param>
        /// <returns>True if the close button was clicked</returns>
        public static bool DrawCategoryChip(BlockCategory category)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            {
                GUILayout.Label(category.ToString(), EditorStyles.label, GUILayout.ExpandWidth(false));

                bool closeClicked = GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(20f));

                EditorGUILayout.EndHorizontal();

                return closeClicked;
            }
        }

        /// <summary>
        /// Filters a list of blocks by category and search query.
        /// </summary>
        /// <param name="blocks">The blocks to filter</param>
        /// <param name="category">The category filter (null for "All")</param>
        /// <param name="searchQuery">The search query</param>
        /// <returns>Filtered block list</returns>
        public static List<BlockDefinition> FilterBlocks(
            List<BlockDefinition> blocks,
            BlockCategory? category,
            string searchQuery)
        {
            if (blocks == null)
            {
                return new List<BlockDefinition>();
            }

            // Filter by category
            List<BlockDefinition> filtered = new List<BlockDefinition>(blocks);

            if (category.HasValue)
            {
                filtered.RemoveAll(b => b == null || b.Category != category.Value);
            }

            // Filter by search query
            if (!string.IsNullOrEmpty(searchQuery))
            {
                string query = searchQuery.ToLower();
                filtered.RemoveAll(b =>
                    b == null ||
                    (string.IsNullOrEmpty(b.BlockId) || !b.BlockId.ToLower().Contains(query)) &&
                    (string.IsNullOrEmpty(b.DisplayName) || !b.DisplayName.ToLower().Contains(query))
                );
            }

            return filtered;
        }

        /// <summary>
        /// Draws a compact info box showing block details.
        /// </summary>
        /// <param name="block">The block to display info for</param>
        public static void DrawBlockInfo(BlockDefinition block)
        {
            if (block == null)
            {
                EditorGUILayout.HelpBox("No block selected.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                GUILayout.Label("Block Info", EditorStyles.boldLabel);

                EditorGUILayout.LabelField("ID", block.BlockId);
                EditorGUILayout.LabelField("Name", block.DisplayName);
                EditorGUILayout.LabelField("Category", block.Category.ToString());
                EditorGUILayout.LabelField("Allow Rotation", block.AllowRotation ? "Yes" : "No");
                EditorGUILayout.LabelField("Allow Scaling", block.AllowScaling ? "Yes" : "No");

                if (block.Tags != null && block.Tags.Length > 0)
                {
                    EditorGUILayout.LabelField("Tags", string.Join(", ", block.Tags));
                }
            }
            EditorGUILayout.EndVertical();
        }
    }
}
