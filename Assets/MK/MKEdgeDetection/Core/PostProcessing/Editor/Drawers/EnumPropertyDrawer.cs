/*****************************************************
Copyright © 2024 Michael Kremmel
https://www.michaelkremmel.de
All rights reserved
*****************************************************/

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;

namespace MK.EdgeDetection.PostProcessing.Generic.Editor
{
    public abstract class EnumPropertyDrawer<T> : UnityEditor.PropertyDrawer where T : System.Enum
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {            
            EditorGUI.BeginProperty(position, label, property);
            SerializedProperty valueProperty = property.FindPropertyRelative("_value");
            valueProperty.intValue = (int) (System.Object) EditorGUI.EnumPopup(position, label, (T) (System.Object) valueProperty.intValue);
            EditorGUI.EndProperty();
            property.serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif