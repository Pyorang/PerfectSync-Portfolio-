using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using ithappy.MapEditor;

namespace ithappy.MapEditor.Editor
{
    /// <summary>
    /// Panel for displaying and editing properties of selected blocks.
    /// Shows block position, rotation, scale, and behavior settings.
    /// For single block: shows full details including behavior panel.
    /// For multiple blocks: shows count and group action buttons.
    /// </summary>
    public class PropertiesPanel
    {
        private BehaviorPresetPanel behaviorPanel = new BehaviorPresetPanel();
        private static readonly float RotationButtonWidth = 50f;
        private static readonly float ScaleButtonWidth = 50f;

        /// <summary>
        /// Draws the properties panel for the selected blocks in EditorState.
        /// Must be called every OnGUI frame when blocks are selected.
        /// </summary>
        public void Draw(EditorState editorState, MapRoot mapRoot)
        {
            if (editorState == null || editorState.SelectedBlocks.Count == 0)
            {
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawSelectionHeader(editorState);
            EditorGUILayout.Space(4f);

            if (editorState.SelectedBlocks.Count == 1)
            {
                MapBlock block = editorState.SelectedBlocks[0];
                DrawSingleBlockProperties(block, editorState, mapRoot);
            }
            else
            {
                DrawMultiBlockProperties(editorState, mapRoot);
            }

            EditorGUILayout.Space(4f);
            DrawActionButtons(editorState);

            EndVertical();
        }

        /// <summary>
        /// Draws the selection header showing count and block ID.
        /// </summary>
        private void DrawSelectionHeader(EditorState editorState)
        {
            int count = editorState.SelectedBlocks.Count;

            if (count == 1)
            {
                MapBlock block = editorState.SelectedBlocks[0];
                EditorGUILayout.LabelField($"Selected: 1 block", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Block: {block.BlockId}", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField($"Selected: {count} blocks", EditorStyles.boldLabel);
            }
        }

        /// <summary>
        /// Draws properties for a single selected block.
        /// </summary>
        private void DrawSingleBlockProperties(MapBlock block, EditorState editorState, MapRoot mapRoot)
        {
            if (block == null)
                return;

            DrawPositionProperties(block);
            EditorGUILayout.Space(3f);

            DrawRotationPresets(block, editorState);
            EditorGUILayout.Space(3f);

            DrawScalePresets(block, editorState);
            EditorGUILayout.Space(6f);

            EditorGUILayout.LabelField("Behavior", EditorStyles.boldLabel);
            behaviorPanel.DrawPanel(block);
        }

        /// <summary>
        /// Draws position X, Y, Z fields with Snap button.
        /// </summary>
        private void DrawPositionProperties(MapBlock block)
        {
            EditorGUILayout.LabelField("Position", EditorStyles.boldLabel);

            BeginHorizontal();
            EditorGUILayout.LabelField("X", GUILayout.Width(20f));
            Vector3 pos = block.transform.position;
            float newX = EditorGUILayout.FloatField(pos.x);
            EditorGUILayout.EndHorizontal();

            BeginHorizontal();
            EditorGUILayout.LabelField("Y", GUILayout.Width(20f));
            float newY = EditorGUILayout.FloatField(pos.y);
            EditorGUILayout.EndHorizontal();

            BeginHorizontal();
            EditorGUILayout.LabelField("Z", GUILayout.Width(20f));
            float newZ = EditorGUILayout.FloatField(pos.z);

            if (GUILayout.Button("Snap", EditorStyles.miniButton, GUILayout.Width(40f)))
            {
                Undo.RecordObject(block.transform, "Snap to Grid");
                block.SnapToGrid(1f);
            }
            EditorGUILayout.EndHorizontal();

            // Apply position changes
            Vector3 newPos = new Vector3(newX, newY, newZ);
            if (newPos != pos)
            {
                Undo.RecordObject(block.transform, "Move Block");
                block.transform.position = newPos;
            }
        }

        /// <summary>
        /// Draws rotation preset buttons (0°, 90°, 180°, 270°).
        /// </summary>
        private void DrawRotationPresets(MapBlock block, EditorState editorState)
        {
            EditorGUILayout.LabelField("Rotation", EditorStyles.boldLabel);

            BeginHorizontal();
            DrawRotationButton(block, 0f);
            DrawRotationButton(block, 90f);
            DrawRotationButton(block, 180f);
            DrawRotationButton(block, 270f);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Helper to draw a single rotation preset button.
        /// </summary>
        private void DrawRotationButton(MapBlock block, float angle)
        {
            if (GUILayout.Button($"{angle}°", EditorStyles.miniButton, GUILayout.Width(RotationButtonWidth)))
            {
                Undo.RecordObject(block.transform, "Set Rotation");
                block.SetRotationPreset(angle);
            }
        }

        /// <summary>
        /// Draws scale preset buttons.
        /// </summary>
        private void DrawScalePresets(MapBlock block, EditorState editorState)
        {
            EditorGUILayout.LabelField("Scale", EditorStyles.boldLabel);

            BeginHorizontal();
            DrawScaleButton(block, 0.5f);
            DrawScaleButton(block, 1f);
            DrawScaleButton(block, 1.5f);
            DrawScaleButton(block, 2f);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Helper to draw a single scale preset button.
        /// </summary>
        private void DrawScaleButton(MapBlock block, float scale)
        {
            if (GUILayout.Button($"{scale}x", EditorStyles.miniButton, GUILayout.Width(ScaleButtonWidth)))
            {
                Undo.RecordObject(block.transform, "Set Scale");
                block.transform.localScale = Vector3.one * scale;
            }
        }

        /// <summary>
        /// Draws properties for multiple selected blocks.
        /// </summary>
        private void DrawMultiBlockProperties(EditorState editorState, MapRoot mapRoot)
        {
            EditorGUILayout.Space(3f);
            EditorGUILayout.HelpBox("Multi-selection: Use group actions below to modify all selected blocks.", MessageType.Info);
            EditorGUILayout.Space(3f);
        }

        /// <summary>
        /// Draws action buttons (Duplicate, Delete, Rotate for groups).
        /// </summary>
        private void DrawActionButtons(EditorState editorState)
        {
            BeginHorizontal();

            if (GUILayout.Button("Duplicate", EditorStyles.miniButton))
            {
                Undo.RecordObjects(editorState.SelectedBlocks.ToArray(), "Duplicate Blocks");
                editorState.DuplicateSelected();
            }

            if (GUILayout.Button("Delete", EditorStyles.miniButton))
            {
                if (EditorUtility.DisplayDialog("Delete Blocks",
                    $"Are you sure you want to delete {editorState.SelectedBlocks.Count} block(s)?",
                    "Delete", "Cancel"))
                {
                    Undo.RecordObjects(editorState.SelectedBlocks.ToArray(), "Delete Blocks");
                    editorState.DeleteSelected();
                }
            }

            EditorGUILayout.EndHorizontal();

            if (editorState.SelectedBlocks.Count > 1)
            {
                BeginHorizontal();

                EditorGUILayout.LabelField("Rotate Group:", EditorStyles.miniLabel, GUILayout.Width(100f));

                if (GUILayout.Button("↶ 90° CW", EditorStyles.miniButton))
                {
                    Undo.RecordObjects(editorState.SelectedBlocks.ToArray(), "Rotate Group CW");
                    foreach (MapBlock block in editorState.SelectedBlocks)
                    {
                        Vector3 euler = block.transform.eulerAngles;
                        euler.y += 90f;
                        block.transform.eulerAngles = euler;
                    }
                }

                if (GUILayout.Button("↷ 90° CCW", EditorStyles.miniButton))
                {
                    Undo.RecordObjects(editorState.SelectedBlocks.ToArray(), "Rotate Group CCW");
                    foreach (MapBlock block in editorState.SelectedBlocks)
                    {
                        Vector3 euler = block.transform.eulerAngles;
                        euler.y -= 90f;
                        block.transform.eulerAngles = euler;
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// Helper to match RULE 4: BeginHorizontal.
        /// </summary>
        private void BeginHorizontal()
        {
            EditorGUILayout.BeginHorizontal();
        }

        /// <summary>
        /// Helper to match RULE 4: EndHorizontal.
        /// </summary>
        private void EndHorizontal()
        {
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Helper to match RULE 4: BeginVertical.
        /// </summary>
        private void BeginVertical(params GUILayoutOption[] options)
        {
            EditorGUILayout.BeginVertical(options);
        }

        private void BeginVertical(GUIStyle style, params GUILayoutOption[] options)
        {
            EditorGUILayout.BeginVertical(style, options);
        }

        /// <summary>
        /// Helper to match RULE 4: EndVertical.
        /// </summary>
        private void EndVertical()
        {
            EditorGUILayout.EndVertical();
        }
    }
}
