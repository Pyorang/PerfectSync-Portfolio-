using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class ApplyAllIn1ShaderToAll : EditorWindow
{
    static string SAVE_FOLDER = "Assets/AllIn1_GeneratedMaterials";

    List<GameObject> targetObjects = new List<GameObject>();
    Vector2 scrollPos;

    [MenuItem("Tools/All In 1 - Apply Shader")]
    static void ShowWindow()
    {
        var window = GetWindow<ApplyAllIn1ShaderToAll>("AllIn1 Shader Applier");
        window.minSize = new Vector2(350, 300);
        window.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Target Objects", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "쉐이더를 적용할 오브젝트를 추가하세요.\n각 오브젝트의 모든 자식 오브젝트에도 자동 적용됩니다.",
            MessageType.Info);
        EditorGUILayout.Space(4);

        // --- Drag & Drop Area ---
        Rect dropArea = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "Drag & Drop GameObjects Here");
        HandleDragAndDrop(dropArea);

        EditorGUILayout.Space(4);

        // --- Add from Selection button ---
        if (GUILayout.Button("Add Selected Objects from Hierarchy"))
        {
            foreach (GameObject go in Selection.gameObjects)
            {
                if (!targetObjects.Contains(go))
                    targetObjects.Add(go);
            }
        }

        EditorGUILayout.Space(8);

        // --- Target List ---
        EditorGUILayout.LabelField("Targets (" + targetObjects.Count + ")", EditorStyles.boldLabel);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.MinHeight(100), GUILayout.MaxHeight(300));

        for (int i = targetObjects.Count - 1; i >= 0; i--)
        {
            EditorGUILayout.BeginHorizontal();

            targetObjects[i] = (GameObject)EditorGUILayout.ObjectField(
                targetObjects[i], typeof(GameObject), true);

            if (GUILayout.Button("X", GUILayout.Width(24)))
            {
                targetObjects.RemoveAt(i);
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear All"))
        {
            targetObjects.Clear();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(12);

        // --- Apply Button ---
        GUI.enabled = targetObjects.Count > 0;

        if (GUILayout.Button("Apply Shader", GUILayout.Height(30)))
        {
            ApplyToTargets();
        }

        GUI.enabled = true;
    }

    void HandleDragAndDrop(Rect dropArea)
    {
        Event evt = Event.current;
        if (!dropArea.Contains(evt.mousePosition)) return;

        switch (evt.type)
        {
            case EventType.DragUpdated:
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.Use();
                break;

            case EventType.DragPerform:
                DragAndDrop.AcceptDrag();
                foreach (Object obj in DragAndDrop.objectReferences)
                {
                    GameObject go = obj as GameObject;
                    if (go != null && !targetObjects.Contains(go))
                        targetObjects.Add(go);
                }
                evt.Use();
                break;
        }
    }

    void ApplyToTargets()
    {
        string shaderName = "AllIn13DShader/AllIn13DShader";
        Shader allIn1Shader = Shader.Find(shaderName);
        if (allIn1Shader == null)
        {
            EditorUtility.DisplayDialog("Error", shaderName + " not found!", "OK");
            return;
        }

        if (!AssetDatabase.IsValidFolder(SAVE_FOLDER))
        {
            AssetDatabase.CreateFolder("Assets", "AllIn1_GeneratedMaterials");
        }

        // Collect all renderers from target objects and their children
        List<Renderer> allRenderers = new List<Renderer>();
        foreach (GameObject target in targetObjects)
        {
            if (target == null) continue;
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (!allRenderers.Contains(r))
                    allRenderers.Add(r);
            }
        }

        int newCount = 0;
        int updatedCount = 0;

        foreach (Renderer renderer in allRenderers)
        {
            Undo.RecordObject(renderer, "Apply AllIn1 Shader");
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null) continue;

                string currentShader = materials[i].shader.name;
                bool isAlreadyAllIn1 = currentShader.Contains("AllIn13DShader");

                if (isAlreadyAllIn1)
                {
                    Undo.RecordObject(materials[i], "Update AllIn1 Settings");
                    if (materials[i].shader != allIn1Shader)
                    {
                        materials[i].shader = allIn1Shader;
                        changed = true;
                    }
                    ApplySettings(materials[i]);

                    string existingPath = AssetDatabase.GetAssetPath(materials[i]);
                    if (string.IsNullOrEmpty(existingPath))
                    {
                        SaveMaterialAsset(materials[i], renderer.gameObject.name, i);
                    }
                    else
                    {
                        EditorUtility.SetDirty(materials[i]);
                    }
                    updatedCount++;
                }
                else
                {
                    Material newMat = new Material(allIn1Shader);
                    newMat.name = "MAT_AllIn1_" + renderer.gameObject.name;

                    if (materials[i].HasProperty("_BaseMap"))
                        newMat.SetTexture("_MainTex", materials[i].GetTexture("_BaseMap"));
                    else if (materials[i].HasProperty("_MainTex"))
                        newMat.SetTexture("_MainTex", materials[i].GetTexture("_MainTex"));

                    if (materials[i].HasProperty("_BaseColor"))
                        newMat.SetColor("_Color", materials[i].GetColor("_BaseColor"));
                    else if (materials[i].HasProperty("_Color"))
                        newMat.SetColor("_Color", materials[i].GetColor("_Color"));

                    ApplySettings(newMat);
                    SaveMaterialAsset(newMat, renderer.gameObject.name, i);

                    materials[i] = newMat;
                    newCount++;
                    changed = true;
                }
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = "New: " + newCount
            + "\nUpdated: " + updatedCount
            + "\nTotal renderers: " + allRenderers.Count;
        Debug.Log("[AllIn1] Done - " + msg);
        EditorUtility.DisplayDialog("Result", msg, "OK");
    }

    static void SaveMaterialAsset(Material mat, string objName, int index)
    {
        string safeName = objName.Replace("/", "_").Replace("\\", "_").Replace(" ", "_");
        string path = SAVE_FOLDER + "/MAT_AllIn1_" + safeName + "_" + index + ".mat";

        int counter = 0;
        while (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
        {
            counter++;
            path = SAVE_FOLDER + "/MAT_AllIn1_" + safeName + "_" + index + "_" + counter + ".mat";
        }

        AssetDatabase.CreateAsset(mat, path);
    }

    static void ApplySettings(Material mat)
    {
        // 1. Light Model: Toon
        mat.SetFloat("_LightModel", 2);
        mat.DisableKeyword("_LIGHTMODEL_NONE");
        mat.DisableKeyword("_LIGHTMODEL_CLASSIC");
        mat.DisableKeyword("_LIGHTMODEL_TOONRAMP");
        mat.DisableKeyword("_LIGHTMODEL_HALFLAMBERT");
        mat.DisableKeyword("_LIGHTMODEL_FAKEGI");
        mat.DisableKeyword("_LIGHTMODEL_FASTLIGHTING");
        mat.EnableKeyword("_LIGHTMODEL_TOON");
        mat.SetFloat("_ToonCutoff", 0.433f);
        mat.SetFloat("_ToonSmoothness", 0f);

        // 2. Shading Model: PBR
        mat.SetFloat("_ShadingModel", 1);
        mat.DisableKeyword("_SHADINGMODEL_BASIC");
        mat.EnableKeyword("_SHADINGMODEL_PBR");
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.7f);

        // 3. Specular Model: Toon
        mat.SetFloat("_SpecularModel", 2);
        mat.DisableKeyword("_SPECULARMODEL_NONE");
        mat.DisableKeyword("_SPECULARMODEL_CLASSIC");
        mat.DisableKeyword("_SPECULARMODEL_ANISOTROPIC");
        mat.DisableKeyword("_SPECULARMODEL_ANISOTROPICTOON");
        mat.EnableKeyword("_SPECULARMODEL_TOON");
        mat.SetFloat("_SpecularAtten", 0f);
        mat.SetFloat("_SpecularToonCutoff", 0f);
        mat.SetFloat("_SpecularToonSmoothness", 0f);

        // Shadows
        mat.SetFloat("_CastShadowsOn", 1f);
        mat.EnableKeyword("_CAST_SHADOWS_ON");
        mat.SetFloat("_ReceiveShadows", 1f);
        mat.EnableKeyword("_RECEIVE_SHADOWS_ON");

        // Outline: None
        mat.DisableKeyword("_OUTLINETYPE_CONSTANT");
        mat.DisableKeyword("_OUTLINETYPE_SIMPLE");
        mat.DisableKeyword("_OUTLINETYPE_FADEWITHDISTANCE");
        mat.EnableKeyword("_OUTLINETYPE_NONE");
        mat.SetFloat("_OutlineType", 0f);
    }
}
