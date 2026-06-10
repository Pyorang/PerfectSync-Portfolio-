using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Reflection;
using ithappy.MapEditor;

namespace ithappy.MapEditor.Editor
{
    /// <summary>
    /// Simulates behavior scripts in the editor without entering Play mode.
    /// Stores original transform state and animates behaviors using EditorApplication.update.
    /// </summary>
    public class BehaviorPreview
    {
        private bool isPlaying;
        private double startTime;
        private List<PreviewState> previewStates;

        public bool IsPlaying => isPlaying;

        public BehaviorPreview()
        {
            isPlaying = false;
            previewStates = new List<PreviewState>();
        }

        /// <summary>
        /// Starts preview simulation for the given blocks.
        /// Stores original transforms and subscribes to editor updates.
        /// </summary>
        public void Play(List<MapBlock> blocks)
        {
            if (blocks == null || blocks.Count == 0)
            {
                Debug.LogWarning("No blocks provided for preview.");
                return;
            }

            if (isPlaying)
                Stop();

            previewStates.Clear();
            startTime = EditorApplication.timeSinceStartup;

            foreach (var block in blocks)
            {
                if (block == null)
                    continue;

                var state = new PreviewState
                {
                    block = block,
                    originalPosition = block.transform.position,
                    originalRotation = block.transform.rotation,
                    originalScale = block.transform.localScale
                };

                previewStates.Add(state);
            }

            isPlaying = true;
            EditorApplication.update += EditorUpdate;
        }

        /// <summary>
        /// Stops preview simulation and restores all original transforms.
        /// </summary>
        public void Stop()
        {
            if (!isPlaying)
                return;

            EditorApplication.update -= EditorUpdate;

            // Restore original transforms
            foreach (var state in previewStates)
            {
                if (state.block != null)
                {
                    Undo.RecordObject(state.block.transform, "Restore transform from preview");
                    state.block.transform.position = state.originalPosition;
                    state.block.transform.rotation = state.originalRotation;
                    state.block.transform.localScale = state.originalScale;
                    EditorUtility.SetDirty(state.block.transform);
                }
            }

            previewStates.Clear();
            isPlaying = false;
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Called every frame while preview is active.
        /// Simulates behaviors and updates transforms.
        /// </summary>
        private void EditorUpdate()
        {
            if (!isPlaying || previewStates.Count == 0)
            {
                Stop();
                return;
            }

            double elapsed = EditorApplication.timeSinceStartup - startTime;

            foreach (var state in previewStates)
            {
                if (state.block == null)
                    continue;

                UpdateBlockTransform(state, elapsed);
            }

            SceneView.RepaintAll();
        }

        /// <summary>
        /// Updates a block's transform based on its behavior component.
        /// </summary>
        private void UpdateBlockTransform(PreviewState state, double elapsed)
        {
            GameObject target = state.block.gameObject;

            // Check for OscillatePosition
            var oscillatePos = GetComponentOfType(target, "ithappy.OscillatePosition");
            if (oscillatePos != null)
            {
                UpdateOscillatePosition(state, oscillatePos, elapsed);
                return;
            }

            // Check for OscillateRotation
            var oscillateRot = GetComponentOfType(target, "ithappy.OscillateRotation");
            if (oscillateRot != null)
            {
                UpdateOscillateRotation(state, oscillateRot, elapsed);
                return;
            }

            // Check for RotationScript (ContinuousRotation)
            var rotationScript = GetComponentOfType(target, "ithappy.RotationScript");
            if (rotationScript != null)
            {
                UpdateContinuousRotation(state, rotationScript, elapsed);
                return;
            }

            // Check for OscillateScale
            var oscillateScale = GetComponentOfType(target, "ithappy.OscillateScale");
            if (oscillateScale != null)
            {
                UpdateOscillateScale(state, oscillateScale, elapsed);
                return;
            }
        }

        /// <summary>
        /// Simulates OscillatePosition behavior.
        /// </summary>
        private void UpdateOscillatePosition(PreviewState state, Component component, double elapsed)
        {
            var moveAxisField = component.GetType().GetField("moveAxis",
                BindingFlags.Public | BindingFlags.Instance);
            var distanceField = component.GetType().GetField("moveDistance",
                BindingFlags.Public | BindingFlags.Instance);
            var durationField = component.GetType().GetField("duration",
                BindingFlags.Public | BindingFlags.Instance);

            if (moveAxisField == null || distanceField == null || durationField == null)
                return;

            Vector3 moveAxis = (Vector3)moveAxisField.GetValue(component);
            float moveDistance = (float)distanceField.GetValue(component);
            float duration = (float)durationField.GetValue(component);

            moveAxis = moveAxis.normalized;

            // Calculate ping-pong time
            float halfDuration = duration / 2f;
            float normalizedTime = Mathf.Repeat((float)elapsed, duration);
            float progress = normalizedTime / halfDuration;

            if (progress > 1f)
            {
                progress = 2f - progress;
            }

            progress = Mathf.Clamp01(progress);
            progress = EaseInOut(progress);

            Vector3 newPosition = state.originalPosition + moveAxis * moveDistance * progress;
            state.block.transform.position = newPosition;
            EditorUtility.SetDirty(state.block.transform);
        }

        /// <summary>
        /// Simulates OscillateRotation behavior.
        /// </summary>
        private void UpdateOscillateRotation(PreviewState state, Component component, double elapsed)
        {
            var rotationAxisField = component.GetType().GetField("rotationAxis",
                BindingFlags.Public | BindingFlags.Instance);
            var angleField = component.GetType().GetField("rotationAngle",
                BindingFlags.Public | BindingFlags.Instance);
            var durationField = component.GetType().GetField("duration",
                BindingFlags.Public | BindingFlags.Instance);

            if (rotationAxisField == null || angleField == null || durationField == null)
                return;

            Vector3 rotationAxis = (Vector3)rotationAxisField.GetValue(component);
            float angle = (float)angleField.GetValue(component);
            float duration = (float)durationField.GetValue(component);

            // Calculate ping-pong time
            float halfDuration = duration / 2f;
            float normalizedTime = Mathf.Repeat((float)elapsed, duration);
            float progress = normalizedTime / halfDuration;

            if (progress > 1f)
            {
                progress = 2f - progress;
            }

            progress = Mathf.Clamp01(progress);
            progress = EaseInOut(progress);

            float currentAngle = angle * progress;
            Quaternion newRotation = state.originalRotation * Quaternion.AngleAxis(currentAngle, rotationAxis);
            state.block.transform.rotation = newRotation;
            EditorUtility.SetDirty(state.block.transform);
        }

        /// <summary>
        /// Simulates RotationScript (ContinuousRotation) behavior.
        /// </summary>
        private void UpdateContinuousRotation(PreviewState state, Component component, double elapsed)
        {
            var rotationAxisField = component.GetType().GetField("rotationAxis",
                BindingFlags.Public | BindingFlags.Instance);
            var speedField = component.GetType().GetField("rotationSpeed",
                BindingFlags.Public | BindingFlags.Instance);

            if (rotationAxisField == null || speedField == null)
                return;

            object rotationAxisObj = rotationAxisField.GetValue(component);
            float speed = (float)speedField.GetValue(component);

            // Determine the axis based on RotationAxis enum
            Vector3 axis = Vector3.up;
            if (rotationAxisObj is int enumValue)
            {
                axis = enumValue switch
                {
                    0 => Vector3.right,    // X
                    1 => Vector3.up,       // Y
                    2 => Vector3.forward,  // Z
                    _ => Vector3.up
                };
            }

            float totalRotation = speed * (float)elapsed;
            Quaternion newRotation = state.originalRotation * Quaternion.AngleAxis(totalRotation, axis);
            state.block.transform.rotation = newRotation;
            EditorUtility.SetDirty(state.block.transform);
        }

        /// <summary>
        /// Simulates OscillateScale behavior.
        /// </summary>
        private void UpdateOscillateScale(PreviewState state, Component component, double elapsed)
        {
            var scaleAxisField = component.GetType().GetField("scaleAxis",
                BindingFlags.Public | BindingFlags.Instance);
            var scaleFactorField = component.GetType().GetField("scaleFactor",
                BindingFlags.Public | BindingFlags.Instance);
            var durationField = component.GetType().GetField("duration",
                BindingFlags.Public | BindingFlags.Instance);

            if (scaleAxisField == null || scaleFactorField == null || durationField == null)
                return;

            Vector3 scaleAxis = (Vector3)scaleAxisField.GetValue(component);
            float scaleFactor = (float)scaleFactorField.GetValue(component);
            float duration = (float)durationField.GetValue(component);

            scaleAxis = Vector3.Scale(scaleAxis, Vector3.one); // Normalize to 0-1 range

            // Calculate ping-pong time
            float halfDuration = duration / 2f;
            float normalizedTime = Mathf.Repeat((float)elapsed, duration);
            float progress = normalizedTime / halfDuration;

            if (progress > 1f)
            {
                progress = 2f - progress;
            }

            progress = Mathf.Clamp01(progress);
            progress = EaseInOut(progress);

            // Interpolate scale from original to original * scaleFactor
            Vector3 newScale = state.originalScale +
                Vector3.Scale(scaleAxis.normalized, Vector3.one * (scaleFactor - 1f)) * progress;

            state.block.transform.localScale = newScale;
            EditorUtility.SetDirty(state.block.transform);
        }

        /// <summary>
        /// Easing function matching the runtime scripts (EaseInOut cubic).
        /// </summary>
        private float EaseInOut(float t)
        {
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }

        /// <summary>
        /// Gets a component of a specific type from a GameObject using reflection.
        /// Uses Rule 9 to find types in the ithappy namespace.
        /// </summary>
        private Component GetComponentOfType(GameObject target, string typeName)
        {
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                var type = assembly.GetType(typeName);
                if (type != null)
                {
                    return target.GetComponent(type);
                }
            }

            return null;
        }

        /// <summary>
        /// Stores the original state of a block's transform.
        /// </summary>
        [System.Serializable]
        private struct PreviewState
        {
            public MapBlock block;
            public Vector3 originalPosition;
            public Quaternion originalRotation;
            public Vector3 originalScale;
        }

        /// <summary>
        /// Destructor to ensure cleanup (safety net).
        /// </summary>
        ~BehaviorPreview()
        {
            if (isPlaying)
            {
                EditorApplication.update -= EditorUpdate;
                Stop();
            }
        }
    }
}
