using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using ithappy.MapEditor;

namespace ithappy.MapEditor.Editor
{
    /// <summary>
    /// A utility panel that draws behavior preset UI in an EditorWindow or CustomEditor.
    /// Displays current behavior, preset buttons, parameter controls, and preview controls.
    /// </summary>
    public class BehaviorPresetPanel
    {
        private bool showPanel = true;
        private MapBlock currentBlock;
        private BehaviorPreview behaviorPreview;
        private BehaviorPresetsDatabase presetsDatabase;

        // Cached default presets
        private List<BehaviorPreset> defaultPresets;

        public BehaviorPresetPanel()
        {
            behaviorPreview = new BehaviorPreview();
            defaultPresets = BehaviorPresetsDatabase.CreateDefaultPresets();
        }

        /// <summary>
        /// Draws the behavior preset panel for the given block.
        /// Call from EditorWindow.OnGUI or CustomEditor.OnInspectorGUI.
        /// </summary>
        public void DrawPanel(MapBlock block)
        {
            currentBlock = block;

            if (currentBlock == null)
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header
            EditorGUILayout.LabelField("Behavior", EditorStyles.boldLabel);

            EditorGUILayout.Space(4f);

            // Current behavior display
            DrawCurrentBehaviorSection();

            EditorGUILayout.Space(6f);

            // Preset buttons
            DrawPresetsSection();

            EditorGUILayout.Space(6f);

            // Preview controls
            DrawPreviewSection();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws the section showing the current behavior and remove button.
        /// </summary>
        private void DrawCurrentBehaviorSection()
        {
            BehaviorPreset current = BehaviorPresetsDatabase.GetCurrentBehavior(currentBlock.gameObject);

            if (current != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Current: {current.behaviorType}", EditorStyles.miniLabel);

                if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(60f)))
                {
                    BehaviorPresetsDatabase.RemoveAllBehaviors(currentBlock.gameObject);
                    EditorUtility.SetDirty(currentBlock.gameObject);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4f);

                // Show parameter sliders for current behavior
                DrawParameterSliders(current);
            }
            else
            {
                EditorGUILayout.HelpBox("No behavior assigned", MessageType.Info);
            }
        }

        /// <summary>
        /// Draws the preset selection buttons.
        /// </summary>
        private void DrawPresetsSection()
        {
            EditorGUILayout.LabelField("Presets", EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < defaultPresets.Count && i < 4; i++)
            {
                DrawPresetButton(defaultPresets[i]);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            for (int i = 4; i < defaultPresets.Count; i++)
            {
                DrawPresetButton(defaultPresets[i]);
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draws a single preset button.
        /// </summary>
        private void DrawPresetButton(BehaviorPreset preset)
        {
            string buttonLabel = GetPresetButtonLabel(preset.presetName);

            if (GUILayout.Button(buttonLabel, EditorStyles.miniButton))
            {
                BehaviorPresetsDatabase.ApplyPreset(currentBlock.gameObject, preset);
                EditorUtility.SetDirty(currentBlock.gameObject);
            }
        }

        /// <summary>
        /// Returns a shortened label for preset buttons.
        /// </summary>
        private string GetPresetButtonLabel(string presetName)
        {
            return presetName switch
            {
                "왕복 이동 (상하)" => "왕복↕",
                "왕복 이동 (좌우)" => "왕복↔",
                "왕복 이동 (전후)" => "왕복↗",
                "무한 회전 Y" => "회전Y",
                "무한 회전 X" => "회전X",
                "왕복 회전" => "왕복회전",
                "크기 펄스" => "크기펄스",
                _ => presetName.Substring(0, Mathf.Min(6, presetName.Length))
            };
        }

        /// <summary>
        /// Draws parameter sliders for the current behavior.
        /// </summary>
        private void DrawParameterSliders(BehaviorPreset current)
        {
            Component component = GetBehaviorComponent(currentBlock.gameObject);
            if (component == null)
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Axis selector (for position, rotation, scale)
            if (current.behaviorType == BehaviorType.OscillatePosition ||
                current.behaviorType == BehaviorType.OscillateRotation ||
                current.behaviorType == BehaviorType.OscillateScale)
            {
                DrawAxisSelector(component, current);
            }

            // Distance slider (for position)
            if (current.behaviorType == BehaviorType.OscillatePosition)
            {
                DrawFloatSlider(component, "moveDistance", "Distance", 0.1f, 10f);
            }

            // Angle slider (for rotation)
            if (current.behaviorType == BehaviorType.OscillateRotation)
            {
                DrawFloatSlider(component, "rotationAngle", "Angle", 1f, 180f);
            }

            // Speed slider (for continuous rotation and scale)
            if (current.behaviorType == BehaviorType.ContinuousRotation)
            {
                DrawFloatSlider(component, "rotationSpeed", "Speed", 1f, 360f);
            }
            else if (current.behaviorType == BehaviorType.OscillateScale)
            {
                DrawFloatSlider(component, "scaleFactor", "Scale Factor", 0.5f, 3f);
            }

            // Duration slider (for oscillating behaviors)
            if (current.behaviorType != BehaviorType.ContinuousRotation)
            {
                DrawFloatSlider(component, "duration", "Duration", 0.1f, 5f);
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws an axis selector dropdown.
        /// </summary>
        private void DrawAxisSelector(Component component, BehaviorPreset preset)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Axis", GUILayout.Width(60f));

            // Determine field name based on behavior type
            string fieldName = preset.behaviorType switch
            {
                BehaviorType.OscillatePosition => "moveAxis",
                BehaviorType.OscillateRotation => "rotationAxis",
                BehaviorType.OscillateScale => "scaleAxis",
                _ => null
            };

            if (fieldName != null)
            {
                var field = component.GetType().GetField(fieldName,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (field != null)
                {
                    Vector3 currentAxis = (Vector3)field.GetValue(component);
                    string axisLabel = VectorToAxisLabel(currentAxis);

                    EditorGUILayout.LabelField(axisLabel, EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Converts a Vector3 axis to a human-readable label.
        /// </summary>
        private string VectorToAxisLabel(Vector3 axis)
        {
            if (axis == Vector3.right) return "X (Right)";
            if (axis == Vector3.up) return "Y (Up)";
            if (axis == Vector3.forward) return "Z (Forward)";
            if (axis == Vector3.one) return "XYZ (All)";
            return "Custom";
        }

        /// <summary>
        /// Draws a slider for a float field on a component.
        /// </summary>
        private void DrawFloatSlider(Component component, string fieldName, string label, float min, float max)
        {
            var field = component.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (field == null)
                return;

            float currentValue = (float)field.GetValue(component);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(60f));
            float newValue = EditorGUILayout.Slider(currentValue, min, max);
            EditorGUILayout.EndHorizontal();

            if (!Mathf.Approximately(currentValue, newValue))
            {
                Undo.RecordObject(component, $"Change {label}");
                field.SetValue(component, newValue);
                EditorUtility.SetDirty(component);
            }
        }

        /// <summary>
        /// Draws the preview play/stop buttons.
        /// </summary>
        private void DrawPreviewSection()
        {
            EditorGUILayout.BeginHorizontal();

            if (!behaviorPreview.IsPlaying)
            {
                if (GUILayout.Button("▶ Preview", EditorStyles.miniButtonLeft))
                {
                    behaviorPreview.Play(new List<MapBlock> { currentBlock });
                }
            }
            else
            {
                if (GUILayout.Button("⏹ Stop", EditorStyles.miniButtonLeft))
                {
                    behaviorPreview.Stop();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Gets the behavior component from a GameObject.
        /// </summary>
        private Component GetBehaviorComponent(GameObject target)
        {
            foreach (BehaviorType type in System.Enum.GetValues(typeof(BehaviorType)))
            {
                System.Type componentType = GetComponentTypeForBehavior(type);
                if (componentType == null)
                    continue;

                Component comp = target.GetComponent(componentType);
                if (comp != null)
                    return comp;
            }

            return null;
        }

        /// <summary>
        /// Maps a BehaviorType to its component type using reflection (Rule 9).
        /// </summary>
        private System.Type GetComponentTypeForBehavior(BehaviorType type)
        {
            string typeName = type switch
            {
                BehaviorType.OscillatePosition => "ithappy.OscillatePosition",
                BehaviorType.OscillateRotation => "ithappy.OscillateRotation",
                BehaviorType.ContinuousRotation => "ithappy.RotationScript",
                BehaviorType.OscillateScale => "ithappy.OscillateScale",
                _ => null
            };

            if (string.IsNullOrEmpty(typeName))
                return null;

            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                var foundType = assembly.GetType(typeName);
                if (foundType != null)
                    return foundType;
            }

            return null;
        }
    }
}
