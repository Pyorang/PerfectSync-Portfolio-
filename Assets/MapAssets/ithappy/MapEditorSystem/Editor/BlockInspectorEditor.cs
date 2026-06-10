using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using ithappy.MapEditor;

namespace ithappy.MapEditor.Editor
{
    /// <summary>
    /// Custom inspector editor for the MapBlock component.
    /// Provides intuitive controls for block placement, transformation, duplication, and animation script management.
    /// </summary>
    [CustomEditor(typeof(MapBlock))]
    [CanEditMultipleObjects]
    public class BlockInspectorEditor : UnityEditor.Editor
    {
        private SerializedProperty blockIdProp;
        private SerializedProperty gridPositionProp;
        private GridSnapSystem gridSnapSystem;
        private BlockDatabase cachedDatabase;
        private MapRoot cachedMapRoot;
        private Vector3 customRotation;
        private Vector3 customScale;
        private bool showAdvancedTransform = false;

        private void OnEnable()
        {
            blockIdProp = serializedObject.FindProperty("blockId");
            gridPositionProp = serializedObject.FindProperty("gridPosition");
            gridSnapSystem = new GridSnapSystem();
            cachedDatabase = null;
            cachedMapRoot = null;

            // Cache map root on enable
            if (target is MapBlock mapBlock)
            {
                cachedMapRoot = mapBlock.GetComponentInParent<MapRoot>();
                if (cachedMapRoot != null)
                {
                    gridSnapSystem.gridUnit = cachedMapRoot.GridSize;
                }
                customRotation = mapBlock.transform.eulerAngles;
                customScale = mapBlock.transform.localScale;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            MapBlock mapBlock = target as MapBlock;
            if (mapBlock == null)
                return;

            // Block Info Section
            DrawSectionHeader("Block Info");

            EditorGUILayout.LabelField("Block ID", blockIdProp.stringValue, EditorStyles.textField);

            // Try to load the database for block definition display
            if (cachedDatabase == null)
            {
                cachedDatabase = Resources.Load<BlockDatabase>("BlockDatabase");
            }

            BlockDefinition definition = null;
            if (cachedDatabase != null)
            {
                definition = cachedDatabase.GetById(blockIdProp.stringValue);
            }

            EditorGUILayout.LabelField("Definition", definition != null ? definition.DisplayName : "Not Found");

            // Change Block Dropdown
            if (definition != null && cachedDatabase != null)
            {
                List<BlockDefinition> sameCategoryBlocks = cachedDatabase.GetByCategory(definition.Category);
                string[] blockNames = sameCategoryBlocks.Select(b => b.DisplayName).ToArray();
                int currentIndex = sameCategoryBlocks.IndexOf(definition);

                EditorGUI.BeginChangeCheck();
                int newIndex = EditorGUILayout.Popup("Change Block", currentIndex, blockNames);
                if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < sameCategoryBlocks.Count)
                {
                    Undo.RecordObject(mapBlock, "Change Block Visual");
                    SwapBlockVisual(mapBlock, sameCategoryBlocks[newIndex]);
                }
            }

            EditorGUILayout.Space();

            // Transform Controls Section
            DrawSectionHeader("Transform Controls");

            // Grid Position
            EditorGUI.BeginChangeCheck();
            Vector3Int newGridPos = EditorGUILayout.Vector3IntField("Grid Position", gridPositionProp.vector3IntValue);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mapBlock, "Set Grid Position");
                if (cachedMapRoot != null)
                {
                    Vector3 newWorldPos = new Vector3(
                        newGridPos.x * cachedMapRoot.GridSize,
                        newGridPos.y * cachedMapRoot.GridSize,
                        newGridPos.z * cachedMapRoot.GridSize
                    );
                    mapBlock.transform.position = newWorldPos;
                }
                gridPositionProp.vector3IntValue = newGridPos;
            }

            // Snap to Grid Button
            if (GUILayout.Button("[Snap to Grid]", GUILayout.Height(30)))
            {
                Undo.RecordObject(mapBlock, "Snap to Grid");
                if (cachedMapRoot != null)
                {
                    mapBlock.SnapToGrid(cachedMapRoot.GridSize);
                }
            }

            EditorGUILayout.Space();

            // Rotation Presets
            EditorGUILayout.LabelField("Rotation (Y-axis)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("[0°]")) SetYRotation(mapBlock, 0f);
            if (GUILayout.Button("[90°]")) SetYRotation(mapBlock, 90f);
            if (GUILayout.Button("[180°]")) SetYRotation(mapBlock, 180f);
            if (GUILayout.Button("[270°]")) SetYRotation(mapBlock, 270f);
            EditorGUILayout.EndHorizontal();

            // Custom Rotation
            EditorGUI.BeginChangeCheck();
            customRotation = EditorGUILayout.Vector3Field("Custom Rotation", mapBlock.transform.eulerAngles);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mapBlock, "Set Rotation");
                mapBlock.transform.eulerAngles = customRotation;
            }

            EditorGUILayout.Space();

            // Scale Presets
            EditorGUILayout.LabelField("Scale", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("[0.5x]")) SetScale(mapBlock, Vector3.one * 0.5f);
            if (GUILayout.Button("[1x]")) SetScale(mapBlock, Vector3.one);
            if (GUILayout.Button("[1.5x]")) SetScale(mapBlock, Vector3.one * 1.5f);
            if (GUILayout.Button("[2x]")) SetScale(mapBlock, Vector3.one * 2f);
            EditorGUILayout.EndHorizontal();

            // Custom Scale
            EditorGUI.BeginChangeCheck();
            customScale = EditorGUILayout.Vector3Field("Custom Scale", mapBlock.transform.localScale);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mapBlock, "Set Scale");
                mapBlock.transform.localScale = customScale;
            }

            EditorGUILayout.Space();

            // Action Buttons Section
            DrawSectionHeader("Block Actions");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("[Duplicate →]"))
            {
                DuplicateBlock(mapBlock, Vector3.right * (cachedMapRoot?.GridSize ?? 1f));
            }
            if (GUILayout.Button("[Duplicate ↑]"))
            {
                DuplicateBlock(mapBlock, Vector3.up * (cachedMapRoot?.GridSize ?? 1f));
            }
            if (GUILayout.Button("[Duplicate →→]"))
            {
                DuplicateBlock(mapBlock, Vector3.forward * (cachedMapRoot?.GridSize ?? 1f));
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("[Delete]", GUILayout.Height(30)))
            {
                Undo.DestroyObjectImmediate(mapBlock.gameObject);
            }

            EditorGUILayout.Space();

            // Animation Script Shortcuts Section
            DrawSectionHeader("Animation Scripts");
            EditorGUILayout.LabelField("Add Component", EditorStyles.boldLabel);

            DrawAnimationScriptButton(mapBlock, "OscillatePosition", "[+ Oscillate Position]");
            DrawAnimationScriptButton(mapBlock, "OscillateRotation", "[+ Oscillate Rotation]");
            DrawAnimationScriptButton(mapBlock, "OscillateScale", "[+ Oscillate Scale]");
            DrawAnimationScriptButton(mapBlock, "RotationScript", "[+ Rotation Script]");
            DrawAnimationScriptButton(mapBlock, "BlendShapeAnimator", "[+ Blend Shape Anim]");

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Draws a bold section header with a separator line.
        /// </summary>
        private void DrawSectionHeader(string title)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
        }

        /// <summary>
        /// Sets the Y-axis rotation of a block.
        /// </summary>
        private void SetYRotation(MapBlock mapBlock, float yAngle)
        {
            Undo.RecordObject(mapBlock.transform, "Set Y Rotation");
            mapBlock.SetRotationPreset(yAngle);
            customRotation = mapBlock.transform.eulerAngles;
        }

        /// <summary>
        /// Sets the scale of a block.
        /// </summary>
        private void SetScale(MapBlock mapBlock, Vector3 newScale)
        {
            Undo.RecordObject(mapBlock.transform, "Set Scale");
            mapBlock.transform.localScale = newScale;
            customScale = newScale;
        }

        /// <summary>
        /// Duplicates a block at the given offset position.
        /// </summary>
        private void DuplicateBlock(MapBlock mapBlock, Vector3 offset)
        {
            Vector3 newPos = mapBlock.transform.position + offset;
            GameObject duplicated = Instantiate(
                mapBlock.gameObject,
                newPos,
                mapBlock.transform.rotation,
                mapBlock.transform.parent
            );
            duplicated.name = mapBlock.gameObject.name + " (Clone)";

            MapBlock duplicatedBlock = duplicated.GetComponent<MapBlock>();
            if (duplicatedBlock != null)
            {
                if (cachedMapRoot != null)
                {
                    cachedMapRoot.RegisterBlock(duplicatedBlock);
                    duplicatedBlock.SnapToGrid(cachedMapRoot.GridSize);
                }
            }

            Undo.RegisterCreatedObjectUndo(duplicated, "Duplicate Block");
            Selection.activeGameObject = duplicated;
        }

        /// <summary>
        /// Swaps the visual of a block to a different BlockDefinition while preserving position/rotation.
        /// </summary>
        private void SwapBlockVisual(MapBlock mapBlock, BlockDefinition newDef)
        {
            if (newDef == null || newDef.Prefab == null)
            {
                Debug.LogWarning("Cannot swap to null definition or definition without prefab.");
                return;
            }

            // Store current transform data
            Vector3 currentPos = mapBlock.transform.position;
            Quaternion currentRot = mapBlock.transform.rotation;
            Vector3 currentScale = mapBlock.transform.localScale;

            // Destroy children (the visual mesh/renderer components)
            while (mapBlock.transform.childCount > 0)
            {
                DestroyImmediate(mapBlock.transform.GetChild(0).gameObject);
            }

            // Instantiate the new prefab's children
            foreach (Transform child in newDef.Prefab.transform)
            {
                GameObject newChild = Instantiate(child.gameObject, mapBlock.transform);
                newChild.name = child.name;
            }

            // Restore transform
            mapBlock.transform.position = currentPos;
            mapBlock.transform.rotation = currentRot;
            mapBlock.transform.localScale = currentScale;

            // Update block ID
            mapBlock.SetBlockId(newDef.BlockId);
            EditorUtility.SetDirty(mapBlock);
        }

        /// <summary>
        /// Draws an animation script button that either adds the component or shows it's already present.
        /// </summary>
        private void DrawAnimationScriptButton(MapBlock mapBlock, string componentTypeName, string buttonLabel)
        {
            // Search all loaded assemblies for the type (handles namespace resolution)
            System.Type componentType = null;

            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                componentType = assembly.GetType($"ithappy.{componentTypeName}");
                if (componentType != null)
                    break;

                componentType = assembly.GetType(componentTypeName);
                if (componentType != null)
                    break;
            }

            if (componentType == null)
            {
                EditorGUILayout.LabelField($"{buttonLabel} - Type not found", EditorStyles.miniLabel);
                return;
            }

            Component existingComponent = mapBlock.GetComponent(componentType);

            if (existingComponent != null)
            {
                EditorGUILayout.LabelField($"  Has {componentTypeName}", EditorStyles.miniLabel);
            }
            else
            {
                if (GUILayout.Button(buttonLabel))
                {
                    Undo.RecordObject(mapBlock.gameObject, $"Add {componentTypeName}");
                    mapBlock.gameObject.AddComponent(componentType);
                    EditorUtility.SetDirty(mapBlock.gameObject);
                }
            }
        }
    }
}
