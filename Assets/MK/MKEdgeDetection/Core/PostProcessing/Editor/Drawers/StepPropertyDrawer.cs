/*****************************************************
Copyright © 2024 Michael Kremmel
https://www.michaelkremmel.de
All rights reserved
*****************************************************/

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;

namespace MK.EdgeDetection.PostProcessing.Generic.Editor
{
    [CustomPropertyDrawer(typeof(StepProperty))]
    public class StepPropertyDrawer : UnityEditor.PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            SerializedProperty valueProperty = property.FindPropertyRelative("_value");
            SerializedProperty minValueProperty = property.FindPropertyRelative("_minValue");
            SerializedProperty maxValueProperty = property.FindPropertyRelative("_maxValue");
            valueProperty.intValue = EditorGUI.IntSlider(position, label, valueProperty.intValue, minValueProperty.intValue, maxValueProperty.intValue);
            EditorGUI.EndProperty();
        }
    }
}
#endif