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
    [CustomPropertyDrawer(typeof(RangeProperty))]
    public class RangePropertyDrawer : UnityEditor.PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            SerializedProperty valueProperty = property.FindPropertyRelative("_value");
            SerializedProperty minValueProperty = property.FindPropertyRelative("_minValue");
            SerializedProperty maxValueProperty = property.FindPropertyRelative("_maxValue");
            valueProperty.floatValue = EditorGUI.Slider(position, label, valueProperty.floatValue, minValueProperty.floatValue, maxValueProperty.floatValue);
            EditorGUI.EndProperty();
        }
    }
}
#endif