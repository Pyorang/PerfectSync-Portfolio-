using UnityEngine;
using UnityEditor;
using System;

namespace ithappy.MapEditor.Editor
{
    /// <summary>
    /// Handles grid snapping logic and grid visualization in the Scene View.
    /// Provides utilities for snapping positions, rotations, and scales to a configurable grid.
    /// This is a regular utility class that manages editor-time grid operations.
    /// </summary>
    public class GridSnapSystem
    {
        // Grid spacing configuration
        public float gridUnit = 2f;
        public bool snapEnabled = true;
        public bool showGrid = true;
        public float rotationSnapAngle = 15f;
        public float scaleSnapUnit = 0.25f;
        public int gridExtent = 50;
        public float currentYLevel = 0f;

        // Visualization colors
        public Color gridColor = new Color(1f, 1f, 1f, 0.15f);
        public Color gridHighlightColor = new Color(0.3f, 0.8f, 1f, 0.3f);

        /// <summary>
        /// Snaps a world position to the nearest grid point on all three axes.
        /// </summary>
        /// <param name="worldPos">The world position to snap</param>
        /// <returns>The snapped position</returns>
        public Vector3 SnapPosition(Vector3 worldPos)
        {
            if (!snapEnabled)
                return worldPos;

            return new Vector3(
                Mathf.Round(worldPos.x / gridUnit) * gridUnit,
                Mathf.Round(worldPos.y / gridUnit) * gridUnit,
                Mathf.Round(worldPos.z / gridUnit) * gridUnit
            );
        }

        /// <summary>
        /// Snaps a world position to the grid on X and Z axes only, keeping Y at the current working height.
        /// Useful for placing blocks on a horizontal plane.
        /// </summary>
        /// <param name="worldPos">The world position to snap</param>
        /// <returns>The snapped position with Y set to currentYLevel</returns>
        public Vector3 SnapPositionXZ(Vector3 worldPos)
        {
            if (!snapEnabled)
            {
                worldPos.y = currentYLevel;
                return worldPos;
            }

            return new Vector3(
                Mathf.Round(worldPos.x / gridUnit) * gridUnit,
                currentYLevel,
                Mathf.Round(worldPos.z / gridUnit) * gridUnit
            );
        }

        /// <summary>
        /// Snaps a world position to the grid while accounting for the block's pivot offset.
        /// This ensures blocks snap as expected regardless of their pivot point.
        /// </summary>
        /// <param name="worldPos">The world position to snap</param>
        /// <param name="snapOffset">The block's pivot offset</param>
        /// <returns>The snapped position adjusted for the offset</returns>
        public Vector3 SnapWithOffset(Vector3 worldPos, Vector3 snapOffset)
        {
            if (!snapEnabled)
                return worldPos;

            Vector3 adjustedPos = worldPos - snapOffset;
            Vector3 snappedPos = SnapPositionXZ(adjustedPos);
            return snappedPos + snapOffset;
        }

        /// <summary>
        /// Snaps a rotation angle to the nearest snap increment.
        /// </summary>
        /// <param name="angle">The rotation angle in degrees</param>
        /// <returns>The snapped angle</returns>
        public float SnapRotation(float angle)
        {
            if (!snapEnabled || rotationSnapAngle <= 0)
                return angle;

            return Mathf.Round(angle / rotationSnapAngle) * rotationSnapAngle;
        }

        /// <summary>
        /// Snaps a scale vector to the nearest scale increment, with minimum clamping.
        /// Each component is snapped independently and clamped to a minimum of 0.25.
        /// </summary>
        /// <param name="scale">The scale vector to snap</param>
        /// <returns>The snapped scale vector with minimum clamping applied</returns>
        public Vector3 SnapScale(Vector3 scale)
        {
            if (!snapEnabled || scaleSnapUnit <= 0)
                return scale;

            Vector3 snappedScale = new Vector3(
                Mathf.Round(scale.x / scaleSnapUnit) * scaleSnapUnit,
                Mathf.Round(scale.y / scaleSnapUnit) * scaleSnapUnit,
                Mathf.Round(scale.z / scaleSnapUnit) * scaleSnapUnit
            );

            // Clamp each component to minimum of 0.25
            snappedScale.x = Mathf.Max(snappedScale.x, 0.25f);
            snappedScale.y = Mathf.Max(snappedScale.y, 0.25f);
            snappedScale.z = Mathf.Max(snappedScale.z, 0.25f);

            return snappedScale;
        }

        /// <summary>
        /// Converts a world position to integer grid coordinates.
        /// Useful for map serialization and grid-based lookups.
        /// </summary>
        /// <param name="worldPos">The world position to convert</param>
        /// <returns>The grid coordinates</returns>
        public Vector3Int WorldToGrid(Vector3 worldPos)
        {
            return new Vector3Int(
                Mathf.RoundToInt(worldPos.x / gridUnit),
                Mathf.RoundToInt(worldPos.y / gridUnit),
                Mathf.RoundToInt(worldPos.z / gridUnit)
            );
        }

        /// <summary>
        /// Converts integer grid coordinates back to world position.
        /// </summary>
        /// <param name="gridPos">The grid coordinates to convert</param>
        /// <returns>The world position</returns>
        public Vector3 GridToWorld(Vector3Int gridPos)
        {
            return new Vector3(
                gridPos.x * gridUnit,
                gridPos.y * gridUnit,
                gridPos.z * gridUnit
            );
        }

        /// <summary>
        /// Draws the grid overlay in the Scene View on the XZ plane at the current Y level.
        /// Highlights every 5th line for visual clarity.
        /// </summary>
        /// <param name="sceneView">The current Scene View (used for camera positioning)</param>
        public void DrawGrid(SceneView sceneView)
        {
            if (!showGrid)
                return;

            // Calculate the range of grid lines to draw based on gridExtent
            int halfExtent = gridExtent / 2;
            float minPos = -halfExtent * gridUnit;
            float maxPos = halfExtent * gridUnit;

            Handles.color = gridColor;

            // Draw grid lines parallel to X axis (running along Z direction)
            for (int z = -halfExtent; z <= halfExtent; z++)
            {
                float zPos = z * gridUnit;
                bool isHighlight = (z % 5 == 0);

                if (isHighlight)
                    Handles.color = gridHighlightColor;
                else
                    Handles.color = gridColor;

                Vector3 startPos = new Vector3(minPos, currentYLevel, zPos);
                Vector3 endPos = new Vector3(maxPos, currentYLevel, zPos);
                Handles.DrawLine(startPos, endPos);
            }

            // Draw grid lines parallel to Z axis (running along X direction)
            for (int x = -halfExtent; x <= halfExtent; x++)
            {
                float xPos = x * gridUnit;
                bool isHighlight = (x % 5 == 0);

                if (isHighlight)
                    Handles.color = gridHighlightColor;
                else
                    Handles.color = gridColor;

                Vector3 startPos = new Vector3(xPos, currentYLevel, minPos);
                Vector3 endPos = new Vector3(xPos, currentYLevel, maxPos);
                Handles.DrawLine(startPos, endPos);
            }

            // Reset color for subsequent drawing operations
            Handles.color = Color.white;
        }

        /// <summary>
        /// Draws a small highlighted point at a snap location to indicate snapping feedback.
        /// </summary>
        /// <param name="point">The world position to highlight</param>
        public void DrawSnapPoint(Vector3 point)
        {
            float pointSize = 0.15f;
            Handles.color = gridHighlightColor;
            Handles.SphereHandleCap(0, point, Quaternion.identity, pointSize, EventType.Repaint);
            Handles.color = Color.white;
        }

        /// <summary>
        /// Draws a wireframe box showing the ghost preview of where a block will be placed.
        /// This provides visual feedback during block placement operations.
        /// </summary>
        /// <param name="position">The center position of the block</param>
        /// <param name="size">The size of the block</param>
        public void DrawBlockGhostBounds(Vector3 position, Vector3 size)
        {
            Handles.color = gridHighlightColor;
            Handles.DrawWireCube(position, size);
            Handles.color = Color.white;
        }
    }
}
