using UnityEngine;

namespace ithappy.MapEditor
{
    /// <summary>
    /// MonoBehaviour component that represents a single placed block instance in the map.
    /// Handles conversion to/from serializable data and provides utility methods for positioning.
    /// </summary>
    public class MapBlock : MonoBehaviour
    {
        /// <summary>
        /// Unique identifier linking this block to its definition.
        /// </summary>
        [SerializeField]
        private string blockId;

        /// <summary>
        /// Runtime reference to the block definition (not serialized).
        /// </summary>
        [System.NonSerialized]
        public BlockDefinition definition;

        /// <summary>
        /// Order of placement (used for sorting and layering).
        /// </summary>
        [SerializeField]
        private int placementOrder;

        /// <summary>
        /// Grid-snapped position for alignment purposes.
        /// </summary>
        [SerializeField]
        private Vector3Int gridPosition;

        /// <summary>
        /// Gets the block ID.
        /// </summary>
        public string BlockId => blockId;

        /// <summary>
        /// Gets the placement order.
        /// </summary>
        public int PlacementOrder => placementOrder;

        /// <summary>
        /// Gets the grid position.
        /// </summary>
        public Vector3Int GridPosition => gridPosition;

        /// <summary>
        /// Sets the block ID.
        /// </summary>
        public void SetBlockId(string id)
        {
            blockId = id;
        }

        /// <summary>
        /// Sets the placement order.
        /// </summary>
        public void SetPlacementOrder(int order)
        {
            placementOrder = order;
        }

        /// <summary>
        /// Converts this block instance to serializable data.
        /// </summary>
        public PlacedBlockData ToData()
        {
            if (string.IsNullOrEmpty(blockId))
            {
                Debug.LogWarning($"MapBlock on {gameObject.name} has no blockId set.", this);
            }

            var data = new PlacedBlockData
            {
                blockId = blockId,
                position = transform.position,
                rotation = transform.eulerAngles,
                scale = transform.localScale,
                sortOrder = placementOrder,
                customPropertiesJson = "{}"
            };

            return data;
        }

        /// <summary>
        /// Applies serialized data to this block's transform.
        /// </summary>
        public void FromData(PlacedBlockData data)
        {
            if (data == null)
            {
                Debug.LogError("Attempted to load null PlacedBlockData into MapBlock.", this);
                return;
            }

            blockId = data.blockId;
            placementOrder = data.sortOrder;

            transform.position = data.position.ToVector3();
            transform.eulerAngles = data.rotation.ToVector3();
            transform.localScale = data.scale.ToVector3();

            SnapToGrid(1f); // Default grid size, will be overridden by parent
        }

        /// <summary>
        /// Calculates world-space bounds from all child renderers.
        /// Returns a default 1x1x1 bounds at the transform position if no renderers are found.
        /// </summary>
        public Bounds GetWorldBounds()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();

            if (renderers == null || renderers.Length == 0)
            {
                // Return default bounds centered at this transform
                return new Bounds(transform.position, Vector3.one);
            }

            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combinedBounds.Encapsulate(renderers[i].bounds);
            }

            return combinedBounds;
        }

        /// <summary>
        /// Snaps this block's position to the nearest grid point.
        /// Updates gridPosition accordingly.
        /// </summary>
        public void SnapToGrid(float gridSize)
        {
            if (gridSize <= 0f)
            {
                Debug.LogWarning("Grid size must be positive.", this);
                return;
            }

            Vector3 currentPos = transform.position;
            Vector3 snappedPos = new Vector3(
                Mathf.Round(currentPos.x / gridSize) * gridSize,
                Mathf.Round(currentPos.y / gridSize) * gridSize,
                Mathf.Round(currentPos.z / gridSize) * gridSize
            );

            transform.position = snappedPos;

            // Update grid position (divide by grid size to get grid coordinates)
            gridPosition = new Vector3Int(
                Mathf.RoundToInt(snappedPos.x / gridSize),
                Mathf.RoundToInt(snappedPos.y / gridSize),
                Mathf.RoundToInt(snappedPos.z / gridSize)
            );
        }

        /// <summary>
        /// Sets the Y-axis rotation while preserving position.
        /// </summary>
        /// <param name="yAngle">Rotation angle in degrees around the Y axis.</param>
        public void SetRotationPreset(float yAngle)
        {
            Vector3 currentEuler = transform.eulerAngles;
            currentEuler.y = yAngle;
            transform.eulerAngles = currentEuler;
        }
    }
}
