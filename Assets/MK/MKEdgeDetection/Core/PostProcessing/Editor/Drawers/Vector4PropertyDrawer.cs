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
    [CustomPropertyDrawer(typeof(Vector4Property))]
    public class Vector4PropertyDrawer : UnityEditor.PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            SerializedProperty valueProperty = property.FindPropertyRelative("_value");
            valueProperty.vector4Value = EditorGUI.Vector4Field(position, label, valueProperty.vector4Value);
            EditorGUI.EndProperty();
        }
    }
}
#endif