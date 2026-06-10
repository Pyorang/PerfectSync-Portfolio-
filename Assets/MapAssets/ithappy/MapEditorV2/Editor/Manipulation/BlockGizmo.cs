using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using ithappy.MapEditor;

namespace ithappy.MapEditor.Editor
{
    /// <summary>
    /// Custom gizmo for manipulating placed blocks.
    /// Draws custom handles around selected block(s) and processes drag operations.
    /// </summary>
    public class BlockGizmo
    {
        public enum GizmoTool { Move, Rotate }

        public GizmoTool CurrentTool { get; set; } = GizmoTool.Move;

        /// <summary>
        /// Draws gizmo handles for the given blocks. Call in SceneView Repaint.
        /// Returns true if any handle was interacted with this frame.
        /// </summary>
        public bool DrawGizmos(List<MapBlock> selectedBlocks, SceneView sceneView)
        {
            if (selectedBlocks == null || selectedBlocks.Count == 0)
                return false;

            // Calculate center of all selected blocks
            Vector3 groupCenter = GroupTransform.GetGroupCenter(selectedBlocks);

            // Draw yellow wireframe cube around each selected block
            Color previousColor = Handles.color;
            Handles.color = Color.yellow;

            foreach (MapBlock block in selectedBlocks)
            {
                if (block == null)
                    continue;

                Bounds worldBounds = block.GetWorldBounds();
                Handles.DrawWireCube(worldBounds.center, worldBounds.size);
            }

            Handles.color = previousColor;

            // Handle tool interactions
            bool handled = false;

            if (CurrentTool == GizmoTool.Move)
            {
                Vector3 newCenter = Handles.PositionHandle(groupCenter, Quaternion.identity);
                if (newCenter != groupCenter)
                {
                    Vector3 delta = newCenter - groupCenter;
                    GroupTransform.MoveGroup(selectedBlocks, delta);
                    handled = true;
                }
            }
            else if (CurrentTool == GizmoTool.Rotate)
            {
                Quaternion rotation = Quaternion.Euler(0, 0, 0);
                Quaternion newRotation = Handles.RotationHandle(rotation, groupCenter);

                // Extract Y-axis rotation delta
                if (newRotation != rotation)
                {
                    float yAngleDelta = newRotation.eulerAngles.y;
                    GroupTransform.RotateGroupAroundCenter(selectedBlocks, yAngleDelta);
                    handled = true;
                }
            }

            return handled;
        }
    }
}
