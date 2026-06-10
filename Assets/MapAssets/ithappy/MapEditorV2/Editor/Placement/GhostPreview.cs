using UnityEngine;
using UnityEditor;
using ithappy.MapEditor;

namespace ithappy.MapEditor.Editor
{
    /// <summary>
    /// Renders a semi-transparent copy of the selected block at the placement position.
    /// Creates a hidden GameObject clone that serves as a visual ghost preview without
    /// affecting raycasts or running any runtime logic.
    /// </summary>
    public class GhostPreview
    {
        private GameObject ghostInstance;
        private Material ghostMaterial;
        private bool isActive;

        /// <summary>
        /// Gets whether the ghost preview is currently active.
        /// </summary>
        public bool IsActive => isActive;

        /// <summary>
        /// Creates and activates a ghost preview of the specified block.
        /// </summary>
        /// <param name="block">The block definition to preview.</param>
        public void Activate(BlockDefinition block)
        {
            if (block == null)
            {
                Debug.LogWarning("[GhostPreview] Cannot activate ghost with null BlockDefinition.");
                return;
            }

            if (block.Prefab == null)
            {
                Debug.LogWarning("[GhostPreview] BlockDefinition has no prefab assigned.");
                return;
            }

            // Clean up any existing ghost
            Deactivate();

            // Create a clone of the prefab
            ghostInstance = Object.Instantiate(block.Prefab);
            ghostInstance.name = $"GhostPreview_{block.BlockId}";
            ghostInstance.hideFlags = HideFlags.HideAndDontSave;

            // Create ghost material with valid cyan color
            ghostMaterial = CreateGhostMaterial(new Color(0.3f, 0.8f, 1.0f, 0.4f));

            // Apply ghost material to all renderers
            ApplyGhostMaterial(ghostInstance);

            // Disable colliders and scripts
            DisableComponents(ghostInstance);

            // Set all child objects to "Ignore Raycast" layer
            SetLayerRecursive(ghostInstance, LayerMask.NameToLayer("Ignore Raycast"));

            isActive = true;
        }

        /// <summary>
        /// Deactivates and destroys the ghost preview.
        /// </summary>
        public void Deactivate()
        {
            if (ghostInstance != null)
            {
                Object.DestroyImmediate(ghostInstance);
                ghostInstance = null;
            }

            if (ghostMaterial != null)
            {
                Object.DestroyImmediate(ghostMaterial);
                ghostMaterial = null;
            }

            isActive = false;
        }

        /// <summary>
        /// Updates the position and rotation of the ghost preview.
        /// Should be called every frame to follow the placement cursor.
        /// </summary>
        /// <param name="position">The world position for the ghost.</param>
        /// <param name="rotation">The rotation for the ghost.</param>
        public void UpdateTransform(Vector3 position, Quaternion rotation)
        {
            if (!isActive || ghostInstance == null)
                return;

            ghostInstance.transform.position = position;
            ghostInstance.transform.rotation = rotation;
        }

        /// <summary>
        /// Changes the color of the ghost preview to indicate validity.
        /// Cyan for valid placement, red for invalid.
        /// </summary>
        /// <param name="valid">True for valid (cyan), false for invalid (red).</param>
        public void SetValid(bool valid)
        {
            if (!isActive || ghostMaterial == null)
                return;

            Color newColor = valid
                ? new Color(0.3f, 0.8f, 1.0f, 0.4f)  // Cyan
                : new Color(1.0f, 0.3f, 0.3f, 0.4f); // Red

            ghostMaterial.color = newColor;

            // Update all renderer materials to use the new color
            if (ghostInstance != null)
            {
                Renderer[] renderers = ghostInstance.GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in renderers)
                {
                    renderer.material.color = newColor;
                }
            }
        }

        /// <summary>
        /// Creates a transparent ghost material.
        /// Tries URP first, falls back to Standard shader.
        /// </summary>
        private Material CreateGhostMaterial(Color color)
        {
            Material mat = null;

            // Try URP first
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader != null)
            {
                mat = new Material(urpShader);
                mat.color = color;
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                return mat;
            }

            // Try Simple Lit
            Shader simpleLitShader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (simpleLitShader != null)
            {
                mat = new Material(simpleLitShader);
                mat.color = color;
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                return mat;
            }

            // Fallback to Standard shader
            Shader standardShader = Shader.Find("Standard");
            if (standardShader != null)
            {
                mat = new Material(standardShader);
                mat.color = color;
                mat.SetFloat("_Mode", 3f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = 3000;
                return mat;
            }

            // Last resort: create basic material
            Debug.LogWarning("[GhostPreview] Could not find URP or Standard shaders. Using default material.");
            mat = new Material(Shader.Find("Hidden/InternalErrorShader"));
            mat.color = color;
            return mat;
        }

        /// <summary>
        /// Applies the ghost material to all renderers in the object and its children.
        /// </summary>
        private void ApplyGhostMaterial(GameObject obj)
        {
            if (obj == null || ghostMaterial == null)
                return;

            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                // Use sharedMaterials to avoid material leak in edit mode
                Material[] newMaterials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < newMaterials.Length; i++)
                {
                    newMaterials[i] = ghostMaterial;
                }
                renderer.sharedMaterials = newMaterials;
            }
        }

        /// <summary>
        /// Disables all collider components and MonoBehaviour scripts in the object and its children.
        /// Does not destroy them, just disables them to prevent runtime logic from running.
        /// </summary>
        private void DisableComponents(GameObject obj)
        {
            if (obj == null)
                return;

            // Disable all colliders
            Collider[] colliders = obj.GetComponentsInChildren<Collider>();
            foreach (Collider collider in colliders)
            {
                collider.enabled = false;
            }

            // Disable all MonoBehaviour scripts
            MonoBehaviour[] scripts = obj.GetComponentsInChildren<MonoBehaviour>();
            foreach (MonoBehaviour script in scripts)
            {
                script.enabled = false;
            }
        }

        /// <summary>
        /// Recursively sets the layer for an object and all its children.
        /// </summary>
        private void SetLayerRecursive(GameObject obj, int layer)
        {
            if (obj == null)
                return;

            obj.layer = layer;

            foreach (Transform child in obj.transform)
            {
                SetLayerRecursive(child.gameObject, layer);
            }
        }
    }
}
