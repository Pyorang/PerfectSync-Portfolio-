using UnityEditor;
using UnityEngine;

public class ReplacePrefab : EditorWindow
{
    GameObject newPrefab;

    [MenuItem("Tools/Replace With Prefab")]
    static void ShowWindow() => GetWindow<ReplacePrefab>("Replace Prefab");

    void OnGUI()
    {
        newPrefab = (GameObject)EditorGUILayout.ObjectField("New Prefab (Variant)", newPrefab, typeof(GameObject), false);

        if (GUILayout.Button("Replace Selected") && newPrefab != null)
        {
            foreach (var selected in Selection.gameObjects)
            {
                var newObj = (GameObject)PrefabUtility.InstantiatePrefab(newPrefab);
                newObj.transform.SetParent(selected.transform.parent);
                newObj.transform.position = selected.transform.position;
                newObj.transform.rotation = selected.transform.rotation;
                newObj.transform.localScale = selected.transform.localScale;
                newObj.name = selected.name;

                Undo.RegisterCreatedObjectUndo(newObj, "Replace Prefab");
                Undo.DestroyObjectImmediate(selected);
            }
        }
    }
}