using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using ithappy.MapEditor;

namespace ithappy.MapEditor.Editor
{
    /// <summary>
    /// Reusable panel for browsing and selecting blocks by category and search.
    /// Manages its own cached data and outputs the clicked block via ClickedBlock property.
    /// Called from MapEditorV2Window.OnGUI.
    /// </summary>
    public class BlockBrowserPanel
    {
        // Cached data
        private List<BlockDefinition> cachedBlocks = new List<BlockDefinition>();
        private bool isDirty = true;
        private BlockCategory? currentCategory;
        private string searchQuery = "";
        private Vector2 paletteScroll;
        private Vector2 categoryScroll;

        // Settings
        private static readonly float ThumbnailSize = 80f;
        private static readonly float SidebarWidth = 120f;
        private static readonly float ItemSpacing = 5f;
        private static readonly float ScrollAreaPadding = 5f;

        // Output
        public BlockDefinition ClickedBlock { get; private set; }

        /// <summary>
        /// Draws the entire block browser panel. MUST be called every OnGUI frame unconditionally.
        /// Returns null each frame; check ClickedBlock property to see if a block was selected.
        /// </summary>
        public void Draw(BlockDatabase database, BlockDefinition currentSelection)
        {
            ClickedBlock = null;

            if (database == null)
            {
                EditorGUILayout.HelpBox("Block Database is not assigned.", MessageType.Warning);
                return;
            }

            // Rebuild cache if dirty
            if (isDirty)
            {
                RebuildCache(database);
            }

            BeginHorizontal();
            DrawCategoryPanel(database);
            DrawBlockPalette(database, currentSelection);
            EndHorizontal();
        }

        /// <summary>
        /// Draws the category sidebar on the left.
        /// </summary>
        private void DrawCategoryPanel(BlockDatabase database)
        {
            BeginVertical(GUILayout.Width(SidebarWidth));

            EditorGUILayout.LabelField("Categories", EditorStyles.boldLabel);

            categoryScroll = BeginScrollView(categoryScroll);

            if (DrawCategoryButton("All", null))
            {
                currentCategory = null;
                isDirty = true;
            }

            BlockCategory[] allCategories = database.GetAllCategories();
            foreach (BlockCategory category in allCategories)
            {
                if (DrawCategoryButton(category.ToString(), category))
                {
                    currentCategory = category;
                    isDirty = true;
                }
            }

            EndScrollView();
            EndVertical();
        }

        /// <summary>
        /// Draws a single category button and returns true if clicked.
        /// </summary>
        private bool DrawCategoryButton(string label, BlockCategory? category)
        {
            bool isSelected = (category == null && currentCategory == null) ||
                             (category != null && currentCategory == category);

            GUI.color = isSelected ? Color.cyan : Color.white;
            bool clicked = GUILayout.Button(label, EditorStyles.miniButton);
            GUI.color = Color.white;

            return clicked;
        }

        /// <summary>
        /// Draws the main block palette with search and grid.
        /// </summary>
        private void DrawBlockPalette(BlockDatabase database, BlockDefinition currentSelection)
        {
            BeginVertical();

            DrawSearchField(database);

            paletteScroll = BeginScrollView(paletteScroll);

            if (cachedBlocks.Count == 0)
            {
                EditorGUILayout.LabelField("No blocks found.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                DrawBlockGrid(currentSelection);
            }

            EndScrollView();
            EndVertical();
        }

        /// <summary>
        /// Draws the search text field.
        /// </summary>
        private void DrawSearchField(BlockDatabase database)
        {
            EditorGUILayout.LabelField("Search", EditorStyles.miniLabel);
            string newQuery = EditorGUILayout.TextField(searchQuery, EditorStyles.textField);

            if (newQuery != searchQuery)
            {
                searchQuery = newQuery;
                isDirty = true;
            }

            EditorGUILayout.Space(3f);
        }

        /// <summary>
        /// Draws the block grid with thumbnails.
        /// </summary>
        private void DrawBlockGrid(BlockDefinition currentSelection)
        {
            int blockIndex = 0;

            while (blockIndex < cachedBlocks.Count)
            {
                BeginHorizontal();

                int colsPerRow = Mathf.Max(1, Mathf.FloorToInt((EditorGUIUtility.currentViewWidth - ScrollAreaPadding) / (ThumbnailSize + ItemSpacing)));

                for (int col = 0; col < colsPerRow && blockIndex < cachedBlocks.Count; col++)
                {
                    DrawBlockThumbnail(cachedBlocks[blockIndex], currentSelection);
                    blockIndex++;
                }

                EndHorizontal();
            }
        }

        /// <summary>
        /// Draws a single block thumbnail button.
        /// </summary>
        private void DrawBlockThumbnail(BlockDefinition block, BlockDefinition currentSelection)
        {
            bool isSelected = (currentSelection != null && block.BlockId == currentSelection.BlockId);

            GUI.color = isSelected ? Color.cyan : Color.white;

            Texture2D texture = block.Thumbnail ?? Texture2D.whiteTexture;
            GUIContent content = new GUIContent(texture, block.DisplayName);

            if (GUILayout.Button(content, GUILayout.Width(ThumbnailSize), GUILayout.Height(ThumbnailSize)))
            {
                ClickedBlock = block;
            }

            GUI.color = Color.white;
        }

        /// <summary>
        /// Rebuilds the cached blocks list based on current category and search query.
        /// Called when isDirty is true.
        /// </summary>
        private void RebuildCache(BlockDatabase database)
        {
            cachedBlocks.Clear();

            List<BlockDefinition> filtered = new List<BlockDefinition>();

            if (currentCategory.HasValue)
            {
                filtered = database.GetByCategory(currentCategory.Value);
            }
            else
            {
                filtered = database.GetAllBlocks();
            }

            if (!string.IsNullOrEmpty(searchQuery))
            {
                filtered = database.Search(searchQuery);
                if (currentCategory.HasValue)
                {
                    filtered = filtered.Where(b => b.Category == currentCategory.Value).ToList();
                }
            }

            cachedBlocks = filtered;
            isDirty = false;
        }

        /// <summary>
        /// Helper to match RULE 4: BeginHorizontal with no layout arguments.
        /// </summary>
        private void BeginHorizontal()
        {
            EditorGUILayout.BeginHorizontal();
        }

        /// <summary>
        /// Helper to match RULE 4: EndHorizontal.
        /// </summary>
        private void EndHorizontal()
        {
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Helper to match RULE 4: BeginVertical with width argument.
        /// </summary>
        private void BeginVertical(params GUILayoutOption[] options)
        {
            EditorGUILayout.BeginVertical(options);
        }

        /// <summary>
        /// Helper to match RULE 4: BeginVertical with no layout arguments.
        /// </summary>
        private void BeginVertical()
        {
            EditorGUILayout.BeginVertical();
        }

        /// <summary>
        /// Helper to match RULE 4: EndVertical.
        /// </summary>
        private void EndVertical()
        {
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Helper to match RULE 4: BeginScrollView.
        /// </summary>
        private Vector2 BeginScrollView(Vector2 scrollPos)
        {
            return EditorGUILayout.BeginScrollView(scrollPos);
        }

        /// <summary>
        /// Helper to match RULE 4: EndScrollView.
        /// </summary>
        private void EndScrollView()
        {
            EditorGUILayout.EndScrollView();
        }
    }
}
