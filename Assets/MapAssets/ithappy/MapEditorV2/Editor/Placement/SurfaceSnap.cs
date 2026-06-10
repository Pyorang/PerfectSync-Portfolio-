using UnityEngine;
using UnityEditor;
using ithappy.MapEditor;

namespace ithappy.MapEditor.Editor
{
    /// <summary>
    /// Handles snapping blocks to surfaces of existing blocks via raycasting.
    /// Provides grid snapping, surface snapping, and visualization of snap feedback.
    /// </summary>
    public class SurfaceSnap
    {
        // Configuration
        public float gridUnit = 2f;
        public bool gridSnapEnabled = true;
        public bool surfaceSnapEnabled = true;
        public bool edgeSnapEnabled = true;
        public float edgeSnapThreshold = 0.5f;  // how close to an edge before snapping (world units)
        public float currentYLevel = 0f;

        // Visualization settings
        public Color gridColor = new Color(1f, 1f, 1f, 0.15f);
        public Color gridHighlightColor = new Color(0.3f, 0.8f, 1f, 0.3f);
        public int gridExtent = 50;

        // Result of last snap calculation
        private Vector3 lastSnappedPosition = Vector3.zero;
        private Vector3 lastHitNormal = Vector3.up;
        private bool lastHitSurface = false;
        private bool lastEdgeSnapped = false;
        private bool lastCenterSnappedX = false;
        private bool lastCenterSnappedZ = false;

        public Vector3 LastSnappedPosition => lastSnappedPosition;
        public Vector3 LastHitNormal => lastHitNormal;
        public bool LastHitSurface => lastHitSurface;
        public bool LastEdgeSnapped => lastEdgeSnapped;
        public bool LastCenterSnappedX => lastCenterSnappedX;
        public bool LastCenterSnappedZ => lastCenterSnappedZ;

        /// <summary>
        /// Calculates the snapped placement position from a mouse ray.
        /// 1. Raycast against scene colliders (ignore "Ignore Raycast" layer)
        /// 2. If hit: snap to surface based on hit normal
        /// 3. If no hit: fallback to XZ plane at currentYLevel
        /// 4. If gridSnapEnabled: also snap to grid
        /// </summary>
        /// <param name="ray">The mouse ray from the scene camera.</param>
        /// <param name="blockBounds">The bounds of the block being placed (for offset calculation).</param>
        /// <returns>The snapped world position for placement.</returns>
        public Vector3 CalculateSnappedPosition(Ray ray, Bounds blockBounds)
        {
            lastEdgeSnapped = false;

            // Perform raycast against all colliders except "Ignore Raycast" layer
            int ignoreLayer = LayerMask.NameToLayer("Ignore Raycast");
            int raycastMask = ~(1 << ignoreLayer);

            if (Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, raycastMask))
            {
                lastHitNormal = hit.normal;

                if (surfaceSnapEnabled)
                {
                    Vector3 offsetPosition = CalculateSurfaceOffset(hit.point, hit.normal, blockBounds);
                    lastSnappedPosition = offsetPosition;
                    lastHitSurface = true;

                    if (gridSnapEnabled)
                        lastSnappedPosition = SnapToGrid(lastSnappedPosition);

                    // Edge snap: align to nearby block edges
                    if (edgeSnapEnabled)
                        lastSnappedPosition = SnapToNearbyEdges(lastSnappedPosition, blockBounds);

                    return lastSnappedPosition;
                }
            }

            // Fallback: use XZ plane at currentYLevel
            Vector3 fallbackPosition = RaycastToXZPlane(ray, currentYLevel);
            lastSnappedPosition = fallbackPosition;
            lastHitSurface = false;

            if (gridSnapEnabled)
                lastSnappedPosition = SnapToGrid(lastSnappedPosition);

            if (edgeSnapEnabled)
                lastSnappedPosition = SnapToNearbyEdges(lastSnappedPosition, blockBounds);

            return lastSnappedPosition;
        }

        /// <summary>
        /// Snaps a position to the nearest grid point on all axes.
        /// </summary>
        /// <param name="pos">The position to snap.</param>
        /// <returns>The snapped position.</returns>
        public Vector3 SnapToGrid(Vector3 pos)
        {
            if (!gridSnapEnabled || gridUnit <= 0f)
                return pos;

            return new Vector3(
                Mathf.Round(pos.x / gridUnit) * gridUnit,
                Mathf.Round(pos.y / gridUnit) * gridUnit,
                Mathf.Round(pos.z / gridUnit) * gridUnit
            );
        }

        /// <summary>
        /// Given a hit point and normal, calculates where the block should be placed
        /// so it sits ON the surface (not inside it).
        /// Uses the block's bounds to offset by half-size in the normal direction.
        /// </summary>
        private Vector3 CalculateSurfaceOffset(Vector3 hitPoint, Vector3 hitNormal, Bounds blockBounds)
        {
            // Normalize the hit normal
            hitNormal = hitNormal.normalized;

            // Determine which face was hit by comparing normal to cardinal directions
            float dotUp = Vector3.Dot(hitNormal, Vector3.up);
            float dotDown = Vector3.Dot(hitNormal, Vector3.down);
            float dotRight = Vector3.Dot(hitNormal, Vector3.right);
            float dotLeft = Vector3.Dot(hitNormal, Vector3.left);
            float dotForward = Vector3.Dot(hitNormal, Vector3.forward);
            float dotBack = Vector3.Dot(hitNormal, Vector3.back);

            // Find the maximum dot product to determine which face
            float maxDot = dotUp;
            Vector3 offsetDirection = Vector3.up;

            if (dotDown > maxDot)
            {
                maxDot = dotDown;
                offsetDirection = Vector3.down;
            }
            if (dotRight > maxDot)
            {
                maxDot = dotRight;
                offsetDirection = Vector3.right;
            }
            if (dotLeft > maxDot)
            {
                maxDot = dotLeft;
                offsetDirection = Vector3.left;
            }
            if (dotForward > maxDot)
            {
                maxDot = dotForward;
                offsetDirection = Vector3.forward;
            }
            if (dotBack > maxDot)
            {
                maxDot = dotBack;
                offsetDirection = Vector3.back;
            }

            // Calculate offset based on the determined face and block bounds
            Vector3 offsetAmount = Vector3.zero;

            if (Mathf.Abs(offsetDirection.y - 1f) < 0.01f)
            {
                // Top face
                offsetAmount = Vector3.up * blockBounds.extents.y;
            }
            else if (Mathf.Abs(offsetDirection.y + 1f) < 0.01f)
            {
                // Bottom face
                offsetAmount = Vector3.down * blockBounds.extents.y;
            }
            else if (Mathf.Abs(offsetDirection.x - 1f) < 0.01f)
            {
                // Right face
                offsetAmount = Vector3.right * blockBounds.extents.x;
            }
            else if (Mathf.Abs(offsetDirection.x + 1f) < 0.01f)
            {
                // Left face
                offsetAmount = Vector3.left * blockBounds.extents.x;
            }
            else if (Mathf.Abs(offsetDirection.z - 1f) < 0.01f)
            {
                // Forward face
                offsetAmount = Vector3.forward * blockBounds.extents.z;
            }
            else if (Mathf.Abs(offsetDirection.z + 1f) < 0.01f)
            {
                // Back face
                offsetAmount = Vector3.back * blockBounds.extents.z;
            }

            return hitPoint + offsetAmount;
        }

        // =====================================================================
        // Edge Snap — aligns placement position to nearby block edges/corners
        // =====================================================================

        /// <summary>
        /// Scans nearby placed blocks and snaps the position so that the new block's
        /// edges align perfectly with existing block edges.
        /// Each axis (X, Y, Z) is snapped independently.
        /// </summary>
        private Vector3 SnapToNearbyEdges(Vector3 position, Bounds blockBounds)
        {
            lastCenterSnappedX = false;
            lastCenterSnappedZ = false;

            // Edges of the block being placed
            float newMinX = position.x - blockBounds.extents.x;
            float newMaxX = position.x + blockBounds.extents.x;
            float newCenterX = position.x;
            float newMinY = position.y - blockBounds.extents.y;
            float newMaxY = position.y + blockBounds.extents.y;
            float newMinZ = position.z - blockBounds.extents.z;
            float newMaxZ = position.z + blockBounds.extents.z;
            float newCenterZ = position.z;

            float bestSnapX = position.x;
            float bestSnapY = position.y;
            float bestSnapZ = position.z;
            float bestDistX = edgeSnapThreshold;
            float bestDistY = edgeSnapThreshold;
            float bestDistZ = edgeSnapThreshold;

            // Track whether best snap is center-to-center
            bool bestIsCenterX = false;
            bool bestIsCenterZ = false;

            MapBlock[] allBlocks = Object.FindObjectsByType<MapBlock>(FindObjectsSortMode.None);

            foreach (MapBlock block in allBlocks)
            {
                if (block == null) continue;

                Bounds otherBounds = block.GetWorldBounds();
                Vector3 otherCenter = otherBounds.center;

                // Skip blocks too far away
                if (Vector3.Distance(position, otherCenter) > otherBounds.size.magnitude + blockBounds.size.magnitude + edgeSnapThreshold)
                    continue;

                float otherMinX = otherBounds.min.x;
                float otherMaxX = otherBounds.max.x;
                float otherCenterX = otherCenter.x;
                float otherMinY = otherBounds.min.y;
                float otherMaxY = otherBounds.max.y;
                float otherMinZ = otherBounds.min.z;
                float otherMaxZ = otherBounds.max.z;
                float otherCenterZ = otherCenter.z;

                // === X axis: edge snaps ===
                TrySnap(newMinX, otherMaxX, ref bestSnapX, ref bestDistX, position.x, blockBounds.extents.x, true);
                TrySnap(newMaxX, otherMinX, ref bestSnapX, ref bestDistX, position.x, blockBounds.extents.x, false);
                TrySnap(newMinX, otherMinX, ref bestSnapX, ref bestDistX, position.x, blockBounds.extents.x, true);
                TrySnap(newMaxX, otherMaxX, ref bestSnapX, ref bestDistX, position.x, blockBounds.extents.x, false);

                // === X axis: CENTER-to-CENTER snap ===
                float centerDistX = Mathf.Abs(newCenterX - otherCenterX);
                if (centerDistX < bestDistX)
                {
                    bestDistX = centerDistX;
                    bestSnapX = otherCenterX;  // align centers directly
                    bestIsCenterX = true;
                }

                // === Y axis ===
                TrySnap(newMinY, otherMaxY, ref bestSnapY, ref bestDistY, position.y, blockBounds.extents.y, true);
                TrySnap(newMaxY, otherMinY, ref bestSnapY, ref bestDistY, position.y, blockBounds.extents.y, false);
                TrySnap(newMinY, otherMinY, ref bestSnapY, ref bestDistY, position.y, blockBounds.extents.y, true);
                TrySnap(newMaxY, otherMaxY, ref bestSnapY, ref bestDistY, position.y, blockBounds.extents.y, false);

                // === Z axis: edge snaps ===
                TrySnap(newMinZ, otherMaxZ, ref bestSnapZ, ref bestDistZ, position.z, blockBounds.extents.z, true);
                TrySnap(newMaxZ, otherMinZ, ref bestSnapZ, ref bestDistZ, position.z, blockBounds.extents.z, false);
                TrySnap(newMinZ, otherMinZ, ref bestSnapZ, ref bestDistZ, position.z, blockBounds.extents.z, true);
                TrySnap(newMaxZ, otherMaxZ, ref bestSnapZ, ref bestDistZ, position.z, blockBounds.extents.z, false);

                // === Z axis: CENTER-to-CENTER snap ===
                float centerDistZ = Mathf.Abs(newCenterZ - otherCenterZ);
                if (centerDistZ < bestDistZ)
                {
                    bestDistZ = centerDistZ;
                    bestSnapZ = otherCenterZ;
                    bestIsCenterZ = true;
                }
            }

            Vector3 result = new Vector3(bestSnapX, bestSnapY, bestSnapZ);
            lastEdgeSnapped = (bestDistX < edgeSnapThreshold || bestDistY < edgeSnapThreshold || bestDistZ < edgeSnapThreshold);
            lastCenterSnappedX = bestIsCenterX && bestDistX < edgeSnapThreshold;
            lastCenterSnappedZ = bestIsCenterZ && bestDistZ < edgeSnapThreshold;
            return result;
        }

        /// <summary>
        /// Tries to snap one edge of the new block to one edge of an existing block.
        /// If the distance is less than the current best, updates the best snap position.
        /// </summary>
        /// <param name="newEdge">Edge position of the new block (min or max)</param>
        /// <param name="otherEdge">Edge position of the existing block (min or max)</param>
        /// <param name="bestCenter">Current best center position for this axis</param>
        /// <param name="bestDist">Current best distance for this axis</param>
        /// <param name="currentCenter">Current center position of the new block on this axis</param>
        /// <param name="extent">Half-size of the new block on this axis</param>
        /// <param name="isMin">True if newEdge is the min edge, false if max</param>
        private static void TrySnap(float newEdge, float otherEdge, ref float bestCenter, ref float bestDist,
            float currentCenter, float extent, bool isMin)
        {
            float dist = Mathf.Abs(newEdge - otherEdge);
            if (dist < bestDist)
            {
                bestDist = dist;
                // Calculate what center position would make newEdge == otherEdge
                bestCenter = isMin ? (otherEdge + extent) : (otherEdge - extent);
            }
        }

        /// <summary>
        /// Draws visual guides for edge snapping: dotted lines showing which edges are aligned.
        /// Call during EventType.Repaint.
        /// </summary>
        public void DrawEdgeSnapGuides(Vector3 position, Bounds blockBounds)
        {
            if (!edgeSnapEnabled || !lastEdgeSnapped) return;

            Handles.color = new Color(1f, 1f, 0f, 0.7f);  // yellow guide lines

            MapBlock[] allBlocks = Object.FindObjectsByType<MapBlock>(FindObjectsSortMode.None);

            float newMinX = position.x - blockBounds.extents.x;
            float newMaxX = position.x + blockBounds.extents.x;
            float newMinZ = position.z - blockBounds.extents.z;
            float newMaxZ = position.z + blockBounds.extents.z;

            foreach (MapBlock block in allBlocks)
            {
                if (block == null) continue;
                Bounds other = block.GetWorldBounds();

                // Check if any edges are aligned (within tolerance)
                float tol = 0.01f;

                // X edge alignments
                if (Mathf.Abs(newMinX - other.max.x) < tol || Mathf.Abs(newMaxX - other.min.x) < tol
                    || Mathf.Abs(newMinX - other.min.x) < tol || Mathf.Abs(newMaxX - other.max.x) < tol)
                {
                    float alignedX = Mathf.Abs(newMinX - other.max.x) < tol ? newMinX :
                                     Mathf.Abs(newMaxX - other.min.x) < tol ? newMaxX :
                                     Mathf.Abs(newMinX - other.min.x) < tol ? newMinX : newMaxX;
                    float yMid = position.y;
                    Handles.DrawDottedLine(
                        new Vector3(alignedX, yMid - 2f, position.z),
                        new Vector3(alignedX, yMid + 2f, position.z), 3f);
                }

                // Z edge alignments
                if (Mathf.Abs(newMinZ - other.max.z) < tol || Mathf.Abs(newMaxZ - other.min.z) < tol
                    || Mathf.Abs(newMinZ - other.min.z) < tol || Mathf.Abs(newMaxZ - other.max.z) < tol)
                {
                    float alignedZ = Mathf.Abs(newMinZ - other.max.z) < tol ? newMinZ :
                                     Mathf.Abs(newMaxZ - other.min.z) < tol ? newMaxZ :
                                     Mathf.Abs(newMinZ - other.min.z) < tol ? newMinZ : newMaxZ;
                    float yMid = position.y;
                    Handles.DrawDottedLine(
                        new Vector3(position.x, yMid - 2f, alignedZ),
                        new Vector3(position.x, yMid + 2f, alignedZ), 3f);
                }
            }

            Handles.color = Color.white;
        }

        /// <summary>
        /// Draws center alignment crosshair lines when center snap is active.
        /// Shows a bright green cross through the placement position.
        /// </summary>
        public void DrawCenterSnapGuides(Vector3 position, Bounds blockBounds)
        {
            if (!edgeSnapEnabled) return;

            float lineLength = Mathf.Max(blockBounds.size.x, blockBounds.size.z) + 3f;
            float y = position.y;

            // X center aligned → draw a line along Z axis through the center
            if (lastCenterSnappedX)
            {
                Handles.color = new Color(0f, 1f, 0.5f, 0.9f);  // bright green
                Handles.DrawLine(
                    new Vector3(position.x, y, position.z - lineLength),
                    new Vector3(position.x, y, position.z + lineLength));

                // Also draw a vertical line
                Handles.DrawLine(
                    new Vector3(position.x, y - 1f, position.z),
                    new Vector3(position.x, y + lineLength * 0.5f, position.z));
            }

            // Z center aligned → draw a line along X axis through the center
            if (lastCenterSnappedZ)
            {
                Handles.color = new Color(0f, 1f, 0.5f, 0.9f);
                Handles.DrawLine(
                    new Vector3(position.x - lineLength, y, position.z),
                    new Vector3(position.x + lineLength, y, position.z));

                Handles.DrawLine(
                    new Vector3(position.x, y - 1f, position.z),
                    new Vector3(position.x, y + lineLength * 0.5f, position.z));
            }

            // Both X and Z centered → draw a diamond/cross marker at the center
            if (lastCenterSnappedX && lastCenterSnappedZ)
            {
                Handles.color = new Color(0f, 1f, 0.5f, 1f);
                float markerSize = 0.3f;
                Handles.SphereHandleCap(0, position, Quaternion.identity, markerSize, EventType.Repaint);
            }

            Handles.color = Color.white;
        }

        /// <summary>
        /// Raycasts to find the intersection with the XZ plane at a given Y level.
        /// </summary>
        private Vector3 RaycastToXZPlane(Ray ray, float yLevel)
        {
            // Plane equation: y = yLevel
            // Ray: P = origin + t * direction
            // Solve for t: origin.y + t * direction.y = yLevel
            if (Mathf.Abs(ray.direction.y) < 0.0001f)
            {
                // Ray is parallel to XZ plane
                return ray.origin + ray.direction * 1000f;
            }

            float t = (yLevel - ray.origin.y) / ray.direction.y;
            t = Mathf.Max(t, 0f); // Clamp to forward direction

            return ray.origin + ray.direction * t;
        }

        /// <summary>
        /// Draws the grid overlay in the Scene View on the XZ plane at the current Y level.
        /// Highlights every 5th line for visual clarity.
        /// </summary>
        /// <param name="sceneView">The current Scene View (used for camera positioning).</param>
        public void DrawGrid(SceneView sceneView)
        {
            // Calculate the range of grid lines to draw
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
        /// Draws a visual indicator at the snapped position.
        /// Typically a small sphere or point to show where the block will be placed.
        /// </summary>
        /// <param name="position">The world position to draw the indicator.</param>
        public void DrawSnapIndicator(Vector3 position)
        {
            float pointSize = 0.15f;
            Handles.color = gridHighlightColor;
            Handles.SphereHandleCap(0, position, Quaternion.identity, pointSize, EventType.Repaint);
            Handles.color = Color.white;
        }
    }
}
