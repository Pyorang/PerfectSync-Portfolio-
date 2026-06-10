using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using ithappy.MapEditor;

namespace ithappy.MapEditor.Editor
{
    /// <summary>
    /// Box-select and multi-selection logic for the map editor.
    /// </summary>
    public class MultiSelect
    {
        private bool isDragging;
        private Vector2 dragStartPos;
        private Vector2 dragCurrentPos;

        public bool IsDragging => isDragging;

        /// <summary>
        /// Handles box-select input. Returns list of MapBlocks in the box.
        /// Call during SceneView input processing.
        /// </summary>
        public List<MapBlock> HandleBoxSelect(Event e, SceneView sceneView)
        {
            List<MapBlock> result = new List<MapBlock>();

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                // Check if we're in Idle mode (no modifiers pressed)
                if (!e.shift && !e.control && !e.alt && !e.command)
                {
                    isDragging = true;
                    dragStartPos = e.mousePosition;
                    dragCurrentPos = e.mousePosition;
                    e.Use();
                    return result;
                }
            }
            else if (e.type == EventType.MouseDrag && isDragging)
            {
                dragCurrentPos = e.mousePosition;
                e.Use();
                return result;
            }
            else if (e.type == EventType.MouseUp && isDragging && e.button == 0)
            {
                isDragging = false;
                dragCurrentPos = e.mousePosition;

                // Find all MapBlocks whose screen position falls within the drag rect
                MapBlock[] allBlocks = Object.FindObjectsByType<MapBlock>(FindObjectsSortMode.None);

                Rect selectionRect = GetSelectionRect();

                foreach (MapBlock block in allBlocks)
                {
                    if (block == null)
                        continue;

                    Vector3 blockWorldPos = block.transform.position;
                    Vector2 blockScreenPos = HandleUtility.WorldToGUIPoint(blockWorldPos);

                    if (selectionRect.Contains(blockScreenPos))
                    {
                        result.Add(block);
                    }
                }

                e.Use();
                return result;
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                isDragging = false;
                e.Use();
            }

            return result;
        }

        /// <summary>
        /// Draws the selection rectangle overlay. Call during Repaint.
        /// Uses Handles.BeginGUI + GUI.Box (NOT GUILayout!)
        /// </summary>
        public void DrawSelectionRect()
        {
            if (!isDragging)
                return;

            Handles.BeginGUI();

            Rect selectionRect = GetSelectionRect();
            GUI.Box(selectionRect, "", GetSelectionRectStyle());

            Handles.EndGUI();
        }

        private Rect GetSelectionRect()
        {
            float minX = Mathf.Min(dragStartPos.x, dragCurrentPos.x);
            float maxX = Mathf.Max(dragStartPos.x, dragCurrentPos.x);
            float minY = Mathf.Min(dragStartPos.y, dragCurrentPos.y);
            float maxY = Mathf.Max(dragStartPos.y, dragCurrentPos.y);

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        private GUIStyle GetSelectionRectStyle()
        {
            GUIStyle style = new GUIStyle();
            Texture2D bgTexture = new Texture2D(1, 1);
            bgTexture.SetPixel(0, 0, new Color(0.2f, 0.5f, 1f, 0.3f));
            bgTexture.Apply();
            style.normal.background = bgTexture;

            style.border = new RectOffset(1, 1, 1, 1);

            return style;
        }
    }
}
