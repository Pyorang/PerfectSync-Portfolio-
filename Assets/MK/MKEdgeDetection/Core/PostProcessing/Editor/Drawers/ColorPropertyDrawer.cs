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
    [CustomPropertyDrawer(typeof(ColorProperty))]
    public class ColorPropertyDrawer : UnityEditor.PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            SerializedProperty valueProperty = property.FindPropertyRelative("_value");
            SerializedProperty useAlphaProperty = property.FindPropertyRelative("_useAlpha");
            SerializedProperty useHDRProperty = property.FindPropertyRelative("_useHDR");
            valueProperty.colorValue = EditorGUI.ColorField(position, label, valueProperty.colorValue, true, useAlphaProperty.boolValue, useHDRProperty.boolValue);
            EditorGUI.EndProperty();
        }
    }
}
#endif