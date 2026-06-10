using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Reflection;
using ithappy.MapEditor;

namespace ithappy.MapEditor.Editor
{
    /// <summary>
    /// Represents a single behavior preset with parameters.
    /// </summary>
    [System.Serializable]
    public class BehaviorPreset
    {
        public string presetName;           // "왕복 이동", "무한 회전", etc.
        public string description;          // short description
        public BehaviorType behaviorType;

        // Parameters (only relevant fields are used per type)
        public Vector3 axis = Vector3.up;
        public float distance = 2f;
        public float speed = 1f;
        public float angle = 45f;
        public float duration = 2f;

        public BehaviorPreset()
        {
        }

        public BehaviorPreset(string name, string desc, BehaviorType type)
        {
            presetName = name;
            description = desc;
            behaviorType = type;
        }
    }

    /// <summary>
    /// Enum defining available behavior types.
    /// Maps 1:1 to existing ithappy namespace scripts.
    /// </summary>
    public enum BehaviorType
    {
        OscillatePosition,      // 왕복 이동
        OscillateRotation,      // 왕복 회전
        ContinuousRotation,     // 무한 회전
        OscillateScale,         // 크기 펄스
    }

    /// <summary>
    /// Database of behavior presets. Allows applying presets to GameObjects
    /// and managing behavior components on map blocks.
    /// </summary>
    [CreateAssetMenu(menuName = "MapEditor/Behavior Presets", fileName = "BehaviorPresetsDatabase")]
    public class BehaviorPresetsDatabase : ScriptableObject
    {
        [SerializeField]
        public List<BehaviorPreset> presets = new List<BehaviorPreset>();

        /// <summary>
        /// Creates all default presets for the behavior system.
        /// </summary>
        public static List<BehaviorPreset> CreateDefaultPresets()
        {
            var defaultPresets = new List<BehaviorPreset>
            {
                new BehaviorPreset("왕복 이동 (상하)", "Up-down oscillation", BehaviorType.OscillatePosition)
                {
                    axis = Vector3.up,
                    distance = 2f,
                    duration = 2f
                },
                new BehaviorPreset("왕복 이동 (좌우)", "Left-right oscillation", BehaviorType.OscillatePosition)
                {
                    axis = Vector3.right,
                    distance = 3f,
                    duration = 2f
                },
                new BehaviorPreset("왕복 이동 (전후)", "Forward-backward oscillation", BehaviorType.OscillatePosition)
                {
                    axis = Vector3.forward,
                    distance = 3f,
                    duration = 2f
                },
                new BehaviorPreset("무한 회전 Y", "Continuous Y-axis rotation", BehaviorType.ContinuousRotation)
                {
                    axis = Vector3.up,
                    speed = 50f
                },
                new BehaviorPreset("무한 회전 X", "Continuous X-axis rotation", BehaviorType.ContinuousRotation)
                {
                    axis = Vector3.right,
                    speed = 50f
                },
                new BehaviorPreset("왕복 회전", "Oscillating rotation", BehaviorType.OscillateRotation)
                {
                    axis = Vector3.up,
                    angle = 45f,
                    duration = 2f
                },
                new BehaviorPreset("크기 펄스", "Scale pulse animation", BehaviorType.OscillateScale)
                {
                    axis = Vector3.one,
                    speed = 1.5f,
                    duration = 2f
                }
            };
            return defaultPresets;
        }

        /// <summary>
        /// Applies a preset to a target GameObject by adding the appropriate component
        /// and setting its public fields based on the preset parameters.
        /// </summary>
        public static void ApplyPreset(GameObject target, BehaviorPreset preset)
        {
            if (target == null || preset == null)
            {
                Debug.LogError("Target or preset is null.");
                return;
            }

            // Remove any existing behavior first
            RemoveAllBehaviors(target);

            System.Type componentType = GetComponentTypeForBehavior(preset.behaviorType);
            if (componentType == null)
            {
                Debug.LogError($"Could not find component type for behavior type: {preset.behaviorType}");
                return;
            }

            Component component = target.AddComponent(componentType);
            if (component == null)
            {
                Debug.LogError($"Failed to add component of type {componentType.Name}");
                return;
            }

            Undo.RecordObject(component, $"Apply preset {preset.presetName}");
            ApplyPresetParametersToComponent(component, preset);
            EditorUtility.SetDirty(component);
        }

        /// <summary>
        /// Removes a specific behavior type from the target GameObject.
        /// </summary>
        public static void RemoveBehavior(GameObject target, BehaviorType type)
        {
            if (target == null)
                return;

            System.Type componentType = GetComponentTypeForBehavior(type);
            if (componentType == null)
                return;

            Component component = target.GetComponent(componentType);
            if (component != null)
            {
                Undo.DestroyObjectImmediate(component);
                EditorUtility.SetDirty(target);
            }
        }

        /// <summary>
        /// Removes all behavior components from the target GameObject.
        /// </summary>
        public static void RemoveAllBehaviors(GameObject target)
        {
            if (target == null)
                return;

            foreach (BehaviorType type in System.Enum.GetValues(typeof(BehaviorType)))
            {
                RemoveBehavior(target, type);
            }
        }

        /// <summary>
        /// Gets the current behavior preset applied to a GameObject by checking
        /// which behavior components exist on it.
        /// </summary>
        public static BehaviorPreset GetCurrentBehavior(GameObject target)
        {
            if (target == null)
                return null;

            foreach (BehaviorType type in System.Enum.GetValues(typeof(BehaviorType)))
            {
                System.Type componentType = GetComponentTypeForBehavior(type);
                if (componentType == null)
                    continue;

                Component component = target.GetComponent(componentType);
                if (component != null)
                {
                    var preset = new BehaviorPreset();
                    preset.behaviorType = type;
                    ExtractPresetParametersFromComponent(component, preset);
                    return preset;
                }
            }

            return null;
        }

        /// <summary>
        /// Applies preset parameters to a component instance.
        /// </summary>
        private static void ApplyPresetParametersToComponent(Component component, BehaviorPreset preset)
        {
            var fields = component.GetType().GetFields(
                BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields)
            {
                if (field.Name == "moveAxis" || field.Name == "rotationAxis" || field.Name == "scaleAxis")
                {
                    field.SetValue(component, preset.axis);
                }
                else if (field.Name == "moveDistance")
                {
                    field.SetValue(component, preset.distance);
                }
                else if (field.Name == "rotationSpeed" || field.Name == "rotationAngle")
                {
                    if (field.Name == "rotationSpeed")
                        field.SetValue(component, preset.speed);
                    else if (field.Name == "rotationAngle")
                        field.SetValue(component, preset.angle);
                }
                else if (field.Name == "duration")
                {
                    field.SetValue(component, preset.duration);
                }
                else if (field.Name == "scaleFactor")
                {
                    field.SetValue(component, preset.speed);
                }
            }
        }

        /// <summary>
        /// Extracts preset parameters from a component instance.
        /// </summary>
        private static void ExtractPresetParametersFromComponent(Component component, BehaviorPreset preset)
        {
            var fields = component.GetType().GetFields(
                BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields)
            {
                if (field.Name == "moveAxis" || field.Name == "rotationAxis" || field.Name == "scaleAxis")
                {
                    preset.axis = (Vector3)field.GetValue(component);
                }
                else if (field.Name == "moveDistance")
                {
                    preset.distance = (float)field.GetValue(component);
                }
                else if (field.Name == "rotationSpeed")
                {
                    preset.speed = (float)field.GetValue(component);
                }
                else if (field.Name == "rotationAngle")
                {
                    preset.angle = (float)field.GetValue(component);
                }
                else if (field.Name == "duration")
                {
                    preset.duration = (float)field.GetValue(component);
                }
                else if (field.Name == "scaleFactor")
                {
                    preset.speed = (float)field.GetValue(component);
                }
            }
        }

        /// <summary>
        /// Maps a BehaviorType enum value to its corresponding component System.Type.
        /// Uses reflection to find the type in the ithappy namespace (Rule 9).
        /// </summary>
        private static System.Type GetComponentTypeForBehavior(BehaviorType type)
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

            // Rule 9: Use reflection to find type in assemblies
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                var foundType = assembly.GetType(typeName);
                if (foundType != null)
                    return foundType;
            }

            Debug.LogError($"Could not find type {typeName} in any assembly");
            return null;
        }
    }
}
