using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace ithappy.MapEditor.Editor
{
    /// <summary>
    /// Static helper class that draws additional overlays and visualizations on the Scene View.
    /// Provides methods for drawing grids, block bounds, labels, connections, and HUD information
    /// to enhance the map editing experience in the editor.
    /// </summary>
    public static class MapEditorSceneGUI
    {
        // Colors for visualization
        private static readonly Color BLOCK_BOUNDS_COLOR = new Color(1f, 0.8f, 0f, 1f);
        private static readonly Color BLOCK_LABEL_COLOR = Color.white;
        private static readonly Color BLOCK_CONNECTION_COLOR = new Color(0.5f, 1f, 0.5f, 0.6f);
        private static readonly Color Y_LEVEL_COLOR = new Color(1f, 0f, 0f, 0.2f);
        private static readonly Color HUD_BACKGROUND_COLOR = new Color(0f, 0f, 0f, 0.7f);

        // Constants
        private const float LABEL_DISTANCE_THRESHOLD = 30f;
        private const float LABEL_SIZE = 11f;
        private const float Y_LEVEL_INDICATOR_SIZE = 2f;
        private const int HUD_WIDTH = 280;
        private const int HUD_HEIGHT = 140;
        private const int HUD_PADDING = 10;

        /// <summary>
        /// Draws the complete map overlay including grid, block bounds, labels, and statistics HUD.
        /// Call this from the SceneView.duringSceneGui callback.
        /// </summary>
        public static void DrawMapOverlay(SceneView sceneView, MapRoot map, GridSnapSystem gridSnap)
        {
            if (sceneView == null || gridSnap == null)
                return;

            // Draw the grid at current Y level
            if (gridSnap.showGrid)
            {
                gridSnap.DrawGrid(sceneView);
            }

            // Draw visualizations for the map if it exists
            if (map != null)
            {
                DrawBlockBounds(map, sceneView);
                DrawBlockLabels(map, sceneView);
                DrawBlockConnections(map);
                DrawYLevelIndicator(gridSnap.currentYLevel, sceneView);
                DrawMapStatsHUD(map, gridSnap, sceneView);
            }
        }

        /// <summary>
        /// Draws yellow wireframe boxes around selected blocks to show their bounds.
        /// </summary>
        private static void DrawBlockBounds(MapRoot map, SceneView sceneView)
        {
            if (map == null || map.PlacedBlocks == null)
                return;

            Handles.color = BLOCK_BOUNDS_COLOR;

            foreach (MapBlock block in map.PlacedBlocks)
            {
                if (block == null)
                    continue;

                // Only draw bounds for selected blocks
                if (!IsBlockSelected(block))
                    continue;

                Bounds bounds = block.GetWorldBounds();
                Handles.DrawWireCube(bounds.center, bounds.size);
            }

            Handles.color = Color.white;
        }

        /// <summary>
        /// Draws small text labels showing block IDs near each placed block.
        /// Labels only appear when the camera is close enough.
        /// </summary>
        private static void DrawBlockLabels(MapRoot map, SceneView sceneView)
        {
            if (map == null || map.PlacedBlocks == null || sceneView == null)
                return;

            Vector3 cameraPos = sceneView.camera.transform.position;

            foreach (MapBlock block in map.PlacedBlocks)
            {
                if (block == null)
                    continue;

                Vector3 blockPos = block.transform.position;
                float distanceToCamera = Vector3.Distance(cameraPos, blockPos);

                // Only show labels when camera is close enough
                if (distanceToCamera > LABEL_DISTANCE_THRESHOLD)
                    continue;

                // Draw label above the block
                Vector3 labelPos = blockPos + Vector3.up * LABEL_SIZE;
                Handles.Label(labelPos, block.BlockId, GetLabelStyle());
            }
        }

        /// <summary>
        /// Draws thin dotted lines between adjacent blocks to visualize their connections.
        /// Connects blocks that are within one grid unit of each other.
        /// </summary>
        public static void DrawBlockConnections(MapRoot map)
        {
            if (map == null || map.PlacedBlocks == null || map.PlacedBlocks.Count < 2)
                return;

            Handles.color = BLOCK_CONNECTION_COLOR;

            // For each block, check for adjacent blocks
            for (int i = 0; i < map.PlacedBlocks.Count; i++)
            {
                MapBlock block1 = map.PlacedBlocks[i];
                if (block1 == null)
                    continue;

                Vector3 pos1 = block1.transform.position;

                for (int j = i + 1; j < map.PlacedBlocks.Count; j++)
                {
                    MapBlock block2 = map.PlacedBlocks[j];
                    if (block2 == null)
                        continue;

                    Vector3 pos2 = block2.transform.position;
                    float distance = Vector3.Distance(pos1, pos2);

                    // Draw connection if blocks are adjacent (within 1.5 grid units)
                    if (distance < map.GridSize * 1.5f)
                    {
                        Handles.DrawDottedLine(pos1, pos2, 2f);
                    }
                }
            }

            Handles.color = Color.white;
        }

        /// <summary>
        /// Draws a colored horizontal plane indicator at the current Y level.
        /// Provides visual reference for the placement height.
        /// </summary>
        private static void DrawYLevelIndicator(float yLevel, SceneView sceneView)
        {
            if (sceneView == null || sceneView.camera == null)
                return;

            // Draw a small reference plane at the Y level
            Vector3 cameraPos = sceneView.camera.transform.position;
            Vector3 centerPos = new Vector3(cameraPos.x, yLevel, cameraPos.z);

            Handles.color = Y_LEVEL_COLOR;
            Handles.DrawSolidRectangleWithOutline(
                new Vector3[] {
                    centerPos + Vector3.left * Y_LEVEL_INDICATOR_SIZE + Vector3.back * Y_LEVEL_INDICATOR_SIZE,
                    centerPos + Vector3.right * Y_LEVEL_INDICATOR_SIZE + Vector3.back * Y_LEVEL_INDICATOR_SIZE,
                    centerPos + Vector3.right * Y_LEVEL_INDICATOR_SIZE + Vector3.forward * Y_LEVEL_INDICATOR_SIZE,
                    centerPos + Vector3.left * Y_LEVEL_INDICATOR_SIZE + Vector3.forward * Y_LEVEL_INDICATOR_SIZE
                },
                Y_LEVEL_COLOR,
                new Color(1f, 0f, 0f, 0.3f)
            );

            Handles.color = Color.white;
        }

        /// <summary>
        /// Draws a HUD panel in the top-left corner of the Scene View showing map statistics.
        /// </summary>
        private static void DrawMapStatsHUD(MapRoot map, GridSnapSystem gridSnap, SceneView sceneView)
        {
            if (map == null)
                return;

            Handles.BeginGUI();

            GUILayout.BeginArea(new Rect(HUD_PADDING, HUD_PADDING, HUD_WIDTH, HUD_HEIGHT));

            // Draw semi-transparent background
            GUI.backgroundColor = HUD_BACKGROUND_COLOR;
            GUI.Box(new Rect(0, 0, HUD_WIDTH, HUD_HEIGHT), "");
            GUI.backgroundColor = Color.white;

            // Draw text content with proper formatting
            GUILayout.Label($"<b>Map: {map.MapName}</b>", GetRichTextStyle(12, true));
            GUILayout.Label($"Blocks: {map.BlockCount}", GetRichTextStyle(11, false));
            GUILayout.Label($"Grid Size: {map.GridSize}", GetRichTextStyle(11, false));
            GUILayout.Label($"Current Y: {gridSnap.currentYLevel:F1}", GetRichTextStyle(11, false));

            // Show mode indicator
            string modeText = gridSnap.snapEnabled ? "Snap: ON" : "Snap: OFF";
            GUILayout.Label($"<color=cyan>{modeText}</color>", GetRichTextStyle(11, false));

            GUILayout.EndArea();

            Handles.EndGUI();
        }

        /// <summary>
        /// Checks if a block is currently selected in the scene hierarchy.
        /// </summary>
        private static bool IsBlockSelected(MapBlock block)
        {
            if (block == null)
                return false;

            GameObject[] selectedObjects = Selection.gameObjects;
            foreach (GameObject obj in selectedObjects)
            {
                if (obj == block.gameObject)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Creates a GUI style for regular label text.
        /// </summary>
        private static GUIStyle GetLabelStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)LABEL_SIZE,
                normal = { textColor = BLOCK_LABEL_COLOR },
                alignment = TextAnchor.MiddleCenter
            };
            return style;
        }

        /// <summary>
        /// Creates a GUI style with rich text support for HUD display.
        /// </summary>
        private static GUIStyle GetRichTextStyle(int fontSize, bool bold)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                richText = true,
                normal = { textColor = Color.white },
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal
            };
            return style;
        }
    }
}
