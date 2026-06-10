using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ithappy.MapEditor
{
    /// <summary>
    /// ScriptableObject that defines metadata and properties for a placeable block in the map editor.
    /// Contains information about the block's appearance, behavior, and constraints.
    /// </summary>
    [CreateAssetMenu(fileName = "NewBlockDefinition", menuName = "MapEditor/Block Definition", order = 0)]
    public class BlockDefinition : ScriptableObject
    {
        [SerializeField]
        private string blockId = "block_001";

        [SerializeField]
        private string displayName = "New Block";

        [SerializeField]
        private BlockCategory category = BlockCategory.Platforms;

        [SerializeField]
        private GameObject prefab;

        [SerializeField]
        private Texture2D thumbnail;

        [SerializeField]
        private Vector3 gridSize = Vector3.one;

        [SerializeField]
        private Vector3 snapOffset = Vector3.zero;

        [SerializeField]
        private bool allowRotation = true;

        [SerializeField]
        private float[] allowedRotationAngles = { 0f, 90f, 180f, 270f };

        [SerializeField]
        private bool allowScaling = true;

        [SerializeField]
        private Vector3 minScale = Vector3.one * 0.5f;

        [SerializeField]
        private Vector3 maxScale = Vector3.one * 3f;

        [SerializeField]
        private string[] tags = new string[0];

        /// <summary>
        /// Cache for the computed local bounds of the prefab.
        /// </summary>
        private Bounds? _cachedBounds;

        #region Properties

        public string BlockId => blockId;
        public string DisplayName => displayName;
        public BlockCategory Category => category;
        public GameObject Prefab => prefab;
        public Texture2D Thumbnail => thumbnail;
        public Vector3 GridSize => gridSize;
        public Vector3 SnapOffset => snapOffset;
        public bool AllowRotation => allowRotation;
        public float[] AllowedRotationAngles => allowedRotationAngles;
        public bool AllowScaling => allowScaling;
        public Vector3 MinScale => minScale;
        public Vector3 MaxScale => maxScale;
        public string[] Tags => tags;

        #endregion

        /// <summary>
        /// Gets the local bounds of the block by computing bounds from all renderers in the prefab.
        /// The result is cached for performance.
        /// </summary>
        /// <returns>A Bounds object encompassing all rendered geometry. Returns a default bounds if prefab is null.</returns>
        public Bounds GetLocalBounds()
        {
            // Return cached bounds if available
            if (_cachedBounds.HasValue)
            {
                return _cachedBounds.Value;
            }

            // Return default bounds if no prefab is assigned
            if (prefab == null)
            {
                return new Bounds(Vector3.zero, Vector3.one);
            }

            // Collect all renderers in the prefab and compute combined bounds
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                // If no renderers found, use a unit cube as fallback
                _cachedBounds = new Bounds(Vector3.zero, Vector3.one);
                return _cachedBounds.Value;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            _cachedBounds = bounds;
            return _cachedBounds.Value;
        }

        /// <summary>
        /// Checks whether this block definition contains a specific tag.
        /// Tag comparison is case-insensitive.
        /// </summary>
        /// <param name="tag">The tag to search for.</param>
        /// <returns>True if the tag exists in this definition; false otherwise.</returns>
        public bool HasTag(string tag)
        {
            if (string.IsNullOrEmpty(tag) || tags == null || tags.Length == 0)
            {
                return false;
            }

            return tags.Any(t => !string.IsNullOrEmpty(t) && t.Equals(tag, System.StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Clears the cached bounds so they are recalculated on the next call to GetLocalBounds().
        /// Useful after prefab modifications.
        /// </summary>
        public void ClearBoundsCache()
        {
            _cachedBounds = null;
        }
    }
}
