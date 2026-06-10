using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomPropertyDrawer(typeof(SerializableDictionary<,>), true)]
public class SerializableDictionaryDrawer : PropertyDrawer
{
    private bool _hasDuplicate;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // 내부 리스트인 _pairs 필드를 찾음
        SerializedProperty pairsProp = property.FindPropertyRelative("_pairs");
        
        if (pairsProp == null)
        {
            EditorGUI.PropertyField(position, property, label, true);
            return;
        }

        // 1. 중복 체크 실행
        _hasDuplicate = CheckForDuplicates(pairsProp);

        // 2. 중복 시 경고창 표시
        if (_hasDuplicate)
        {
            Rect helpBoxRect = new Rect(position.x, position.y, position.width, 28);
            EditorGUI.HelpBox(helpBoxRect, "⚠️ 중복된 Key가 존재합니다! (데이터 유실 주의)", MessageType.Error);
            position.y += 32;
        }

        // 3. 리스트 추가 이벤트 감지 시작
        int oldSize = pairsProp.arraySize;
        EditorGUI.BeginChangeCheck();

        // 리스트 그리기
        EditorGUI.PropertyField(position, pairsProp, label, true);

        // 4. 값이 변경되었을 때 (특히 리스트 사이즈가 늘어났을 때) 로직 수행
        if (EditorGUI.EndChangeCheck())
        {
            if (pairsProp.arraySize > oldSize)
            {
                ApplyAutoIncrement(pairsProp);
            }
        }
    }

    // 자동 +1 로직 (int, enum, string 대응)
    private void ApplyAutoIncrement(SerializedProperty listProp)
    {
        int lastIndex = listProp.arraySize - 1;
        int prevIndex = lastIndex - 1;
        if (prevIndex < 0) return;

        var lastKeyProp = listProp.GetArrayElementAtIndex(lastIndex).FindPropertyRelative("Key");
        var prevKeyProp = listProp.GetArrayElementAtIndex(prevIndex).FindPropertyRelative("Key");

        switch (lastKeyProp.propertyType)
        {
            case SerializedPropertyType.Integer:
                lastKeyProp.longValue = prevKeyProp.longValue + 1;
                break;
            case SerializedPropertyType.Enum:
                int nextVal = prevKeyProp.enumValueIndex + 1;
                lastKeyProp.enumValueIndex = Mathf.Min(nextVal, lastKeyProp.enumNames.Length - 1);
                break;
            case SerializedPropertyType.String:
                lastKeyProp.stringValue = prevKeyProp.stringValue + "_copy";
                break;
        }
        
        listProp.serializedObject.ApplyModifiedProperties();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        SerializedProperty pairsProp = property.FindPropertyRelative("_pairs");
        float baseHeight = EditorGUI.GetPropertyHeight(pairsProp ?? property, label, true);
        // 중복 시 경고창 높이 추가
        return _hasDuplicate ? baseHeight + 35 : baseHeight;
    }

    private bool CheckForDuplicates(SerializedProperty listProp)
    {
        HashSet<string> keys = new HashSet<string>();
        for (int i = 0; i < listProp.arraySize; i++)
        {
            var keyProp = listProp.GetArrayElementAtIndex(i).FindPropertyRelative("Key");
            if (keyProp == null) continue;

            string keyStr = GetKeyString(keyProp);
            if (keys.Contains(keyStr)) return true;
            keys.Add(keyStr);
        }
        return false;
    }

    private string GetKeyString(SerializedProperty prop)
    {
        return prop.propertyType switch
        {
            SerializedPropertyType.Integer => prop.longValue.ToString(),
            SerializedPropertyType.String => prop.stringValue,
            SerializedPropertyType.Enum => prop.enumValueIndex.ToString(),
            SerializedPropertyType.LayerMask => prop.intValue.ToString(),
            SerializedPropertyType.ObjectReference => prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "null",
            _ => prop.displayName
        };
    }
}
