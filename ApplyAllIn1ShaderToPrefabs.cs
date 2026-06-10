using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class ApplyAllIn1ShaderToPrefabs : EditorWindow
{
    static string SAVE_FOLDER = "Assets/AllIn1_GeneratedMaterials";
    const string DEFAULT_PREFAB_FOLDER = "Assets/03. Prefabs/Gimmicks";

    List<GameObject> targetPrefabs = new List<GameObject>();
    string folderPath = DEFAULT_PREFAB_FOLDER;
    bool includeSubfolders = true;
    bool skipVariants = true;
    Vector2 scrollPos;

    // --- Settings ---
    enum LightModelType { None = 0, Classic = 1, Toon = 2, ToonRamp = 3, HalfLambert = 4, FakeGI = 5, FastLighting = 6 }
    enum ShadingModelType { Basic = 0, PBR = 1 }
    enum SpecularModelType { None = 0, Classic = 1, Toon = 2, Anisotropic = 3, AnisotropicToon = 4 }
    enum OutlineTypeEnum { None = 0, Constant = 1, Simple = 2, FadeWithDistance = 3 }

    LightModelType lightModel = LightModelType.Toon;
    float toonCutoff = 0.433f;
    float toonSmoothness = 0f;

    ShadingModelType shadingModel = ShadingModelType.PBR;
    float metallic = 0f;
    float smoothness = 0.7f;

    SpecularModelType specularModel = SpecularModelType.Toon;
    float specularAtten = 0f;
    float specularToonCutoff = 0f;
    float specularToonSmoothness = 0f;

    bool castShadows = true;
    bool receiveShadows = true;

    OutlineTypeEnum outlineType = OutlineTypeEnum.None;

    // --- Glitch Effect ---
    bool glitchEnabled = false;
    float glitchTiling = 5f;
    float glitchAmount = 0.5f;
    Vector3 glitchOffset = new Vector3(-0.5f, 0f, 0f);
    float glitchSpeed = 2.5f;
    bool glitchWorldSpace = true;

    bool showSettings = true;

    // EditorPrefs keys (기존 창과 분리)
    const string PREF_PREFIX = "AllIn1ShaderApplier_Prefab_";

    [MenuItem("Tools/All In 1 - Apply Shader to Prefabs")]
    static void ShowWindow()
    {
        var window = GetWindow<ApplyAllIn1ShaderToPrefabs>("AllIn1 Shader (Prefabs)");
        window.minSize = new Vector2(400, 600);
        window.Show();
    }

    void OnEnable()  { LoadSettings(); }
    void OnDisable() { SaveSettings(); }

    void LoadSettings()
    {
        folderPath = EditorPrefs.GetString(PREF_PREFIX + "FolderPath", DEFAULT_PREFAB_FOLDER);
        includeSubfolders = EditorPrefs.GetBool(PREF_PREFIX + "IncludeSubfolders", true);
        skipVariants = EditorPrefs.GetBool(PREF_PREFIX + "SkipVariants", true);

        lightModel = (LightModelType)EditorPrefs.GetInt(PREF_PREFIX + "LightModel", (int)LightModelType.Toon);
        toonCutoff = EditorPrefs.GetFloat(PREF_PREFIX + "ToonCutoff", 0.433f);
        toonSmoothness = EditorPrefs.GetFloat(PREF_PREFIX + "ToonSmoothness", 0f);

        shadingModel = (ShadingModelType)EditorPrefs.GetInt(PREF_PREFIX + "ShadingModel", (int)ShadingModelType.PBR);
        metallic = EditorPrefs.GetFloat(PREF_PREFIX + "Metallic", 0f);
        smoothness = EditorPrefs.GetFloat(PREF_PREFIX + "Smoothness", 0.7f);

        specularModel = (SpecularModelType)EditorPrefs.GetInt(PREF_PREFIX + "SpecularModel", (int)SpecularModelType.Toon);
        specularAtten = EditorPrefs.GetFloat(PREF_PREFIX + "SpecularAtten", 0f);
        specularToonCutoff = EditorPrefs.GetFloat(PREF_PREFIX + "SpecularToonCutoff", 0f);
        specularToonSmoothness = EditorPrefs.GetFloat(PREF_PREFIX + "SpecularToonSmoothness", 0f);

        castShadows = EditorPrefs.GetBool(PREF_PREFIX + "CastShadows", true);
        receiveShadows = EditorPrefs.GetBool(PREF_PREFIX + "ReceiveShadows", true);

        outlineType = (OutlineTypeEnum)EditorPrefs.GetInt(PREF_PREFIX + "OutlineType", (int)OutlineTypeEnum.None);

        glitchEnabled = EditorPrefs.GetBool(PREF_PREFIX + "GlitchEnabled", false);
        glitchTiling = EditorPrefs.GetFloat(PREF_PREFIX + "GlitchTiling", 5f);
        glitchAmount = EditorPrefs.GetFloat(PREF_PREFIX + "GlitchAmount", 0.5f);
        glitchOffset.x = EditorPrefs.GetFloat(PREF_PREFIX + "GlitchOffsetX", -0.5f);
        glitchOffset.y = EditorPrefs.GetFloat(PREF_PREFIX + "GlitchOffsetY", 0f);
        glitchOffset.z = EditorPrefs.GetFloat(PREF_PREFIX + "GlitchOffsetZ", 0f);
        glitchSpeed = EditorPrefs.GetFloat(PREF_PREFIX + "GlitchSpeed", 2.5f);
        glitchWorldSpace = EditorPrefs.GetBool(PREF_PREFIX + "GlitchWorldSpace", true);
    }

    void SaveSettings()
    {
        EditorPrefs.SetString(PREF_PREFIX + "FolderPath", folderPath);
        EditorPrefs.SetBool(PREF_PREFIX + "IncludeSubfolders", includeSubfolders);
        EditorPrefs.SetBool(PREF_PREFIX + "SkipVariants", skipVariants);

        EditorPrefs.SetInt(PREF_PREFIX + "LightModel", (int)lightModel);
        EditorPrefs.SetFloat(PREF_PREFIX + "ToonCutoff", toonCutoff);
        EditorPrefs.SetFloat(PREF_PREFIX + "ToonSmoothness", toonSmoothness);

        EditorPrefs.SetInt(PREF_PREFIX + "ShadingModel", (int)shadingModel);
        EditorPrefs.SetFloat(PREF_PREFIX + "Metallic", metallic);
        EditorPrefs.SetFloat(PREF_PREFIX + "Smoothness", smoothness);

        EditorPrefs.SetInt(PREF_PREFIX + "SpecularModel", (int)specularModel);
        EditorPrefs.SetFloat(PREF_PREFIX + "SpecularAtten", specularAtten);
        EditorPrefs.SetFloat(PREF_PREFIX + "SpecularToonCutoff", specularToonCutoff);
        EditorPrefs.SetFloat(PREF_PREFIX + "SpecularToonSmoothness", specularToonSmoothness);

        EditorPrefs.SetBool(PREF_PREFIX + "CastShadows", castShadows);
        EditorPrefs.SetBool(PREF_PREFIX + "ReceiveShadows", receiveShadows);

        EditorPrefs.SetInt(PREF_PREFIX + "OutlineType", (int)outlineType);

        EditorPrefs.SetBool(PREF_PREFIX + "GlitchEnabled", glitchEnabled);
        EditorPrefs.SetFloat(PREF_PREFIX + "GlitchTiling", glitchTiling);
        EditorPrefs.SetFloat(PREF_PREFIX + "GlitchAmount", glitchAmount);
        EditorPrefs.SetFloat(PREF_PREFIX + "GlitchOffsetX", glitchOffset.x);
        EditorPrefs.SetFloat(PREF_PREFIX + "GlitchOffsetY", glitchOffset.y);
        EditorPrefs.SetFloat(PREF_PREFIX + "GlitchOffsetZ", glitchOffset.z);
        EditorPrefs.SetFloat(PREF_PREFIX + "GlitchSpeed", glitchSpeed);
        EditorPrefs.SetBool(PREF_PREFIX + "GlitchWorldSpace", glitchWorldSpace);
    }

    void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Target Prefabs", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "프로젝트 창의 프리팹 에셋을 대상으로 합니다.\n" +
            "프리팹의 자식까지 모두 순회하여 쉐이더를 적용합니다.\n" +
            "하이어라키가 아닌 '프로젝트 에셋' 자체가 수정됩니다.",
            MessageType.Info);

        EditorGUILayout.Space(4);

        // --- Folder-based add ---
        EditorGUILayout.LabelField("Folder", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        folderPath = EditorGUILayout.TextField(folderPath);
        if (GUILayout.Button("...", GUILayout.Width(28)))
        {
            string abs = EditorUtility.OpenFolderPanel("Select Prefab Folder", folderPath, "");
            if (!string.IsNullOrEmpty(abs))
            {
                string proj = Application.dataPath.Replace("\\", "/");
                abs = abs.Replace("\\", "/");
                if (abs.StartsWith(proj))
                    folderPath = "Assets" + abs.Substring(proj.Length);
                else
                    EditorUtility.DisplayDialog("Invalid Folder", "프로젝트 Assets 폴더 내부에서 선택해주세요.", "OK");
            }
        }
        EditorGUILayout.EndHorizontal();

        includeSubfolders = EditorGUILayout.Toggle("Include Subfolders", includeSubfolders);
        skipVariants = EditorGUILayout.Toggle(new GUIContent("Skip Prefab Variants",
            "Variant 프리팹은 베이스의 변경을 자동 상속하므로 보통 건너뛰는 게 안전합니다."), skipVariants);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add All Prefabs in Folder"))
        {
            AddPrefabsFromFolder(folderPath, includeSubfolders);
        }
        if (GUILayout.Button("Add Selected from Project"))
        {
            foreach (Object obj in Selection.objects)
            {
                GameObject go = obj as GameObject;
                if (go == null) continue;
                string path = AssetDatabase.GetAssetPath(go);
                if (string.IsNullOrEmpty(path)) continue;
                if (PrefabUtility.GetPrefabAssetType(go) == PrefabAssetType.NotAPrefab) continue;
                if (!targetPrefabs.Contains(go))
                    targetPrefabs.Add(go);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // --- Drag & Drop Area ---
        Rect dropArea = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "Drag & Drop Prefab Assets Here");
        HandleDragAndDrop(dropArea);

        EditorGUILayout.Space(8);

        // --- Target List ---
        EditorGUILayout.LabelField("Targets (" + targetPrefabs.Count + ")", EditorStyles.boldLabel);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.MinHeight(60), GUILayout.MaxHeight(200));
        for (int i = targetPrefabs.Count - 1; i >= 0; i--)
        {
            EditorGUILayout.BeginHorizontal();
            targetPrefabs[i] = (GameObject)EditorGUILayout.ObjectField(targetPrefabs[i], typeof(GameObject), false);
            if (GUILayout.Button("X", GUILayout.Width(24)))
                targetPrefabs.RemoveAt(i);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("Clear All")) targetPrefabs.Clear();

        EditorGUILayout.Space(8);

        // ========================
        // --- Settings Panel ---
        // ========================
        showSettings = EditorGUILayout.Foldout(showSettings, "Shader Settings", true, EditorStyles.foldoutHeader);

        if (showSettings)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Light Model", EditorStyles.boldLabel);
            lightModel = (LightModelType)EditorGUILayout.EnumPopup("Type", lightModel);
            if (lightModel == LightModelType.Toon)
            {
                toonCutoff = EditorGUILayout.Slider("Toon Cutoff", toonCutoff, 0f, 1f);
                toonSmoothness = EditorGUILayout.Slider("Toon Smoothness", toonSmoothness, 0f, 1f);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Shading Model", EditorStyles.boldLabel);
            shadingModel = (ShadingModelType)EditorGUILayout.EnumPopup("Type", shadingModel);
            if (shadingModel == ShadingModelType.PBR)
            {
                metallic = EditorGUILayout.Slider("Metallic", metallic, 0f, 1f);
                smoothness = EditorGUILayout.Slider("Smoothness", smoothness, 0f, 1f);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Specular Model", EditorStyles.boldLabel);
            specularModel = (SpecularModelType)EditorGUILayout.EnumPopup("Type", specularModel);
            if (specularModel != SpecularModelType.None)
                specularAtten = EditorGUILayout.Slider("Specular Atten", specularAtten, 0f, 1f);
            if (specularModel == SpecularModelType.Toon)
            {
                specularToonCutoff = EditorGUILayout.Slider("Specular Toon Cutoff", specularToonCutoff, 0f, 1f);
                specularToonSmoothness = EditorGUILayout.Slider("Specular Toon Smoothness", specularToonSmoothness, 0f, 1f);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Shadows", EditorStyles.boldLabel);
            castShadows = EditorGUILayout.Toggle("Cast Shadows", castShadows);
            receiveShadows = EditorGUILayout.Toggle("Receive Shadows", receiveShadows);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Outline", EditorStyles.boldLabel);
            outlineType = (OutlineTypeEnum)EditorGUILayout.EnumPopup("Type", outlineType);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Glitch Effect (Mesh)", EditorStyles.boldLabel);
            glitchEnabled = EditorGUILayout.Toggle("Enable Glitch", glitchEnabled);
            if (glitchEnabled)
            {
                glitchTiling = EditorGUILayout.FloatField("Glitch Tiling", glitchTiling);
                glitchAmount = EditorGUILayout.Slider("Glitch Amount", glitchAmount, 0f, 1f);
                glitchOffset = EditorGUILayout.Vector3Field("Glitch Offset", glitchOffset);
                glitchSpeed = EditorGUILayout.FloatField("Glitch Speed", glitchSpeed);
                glitchWorldSpace = EditorGUILayout.Toggle("Use World Space", glitchWorldSpace);
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset to Defaults")) ResetToDefaults();
            if (GUILayout.Button("Save Settings"))
            {
                SaveSettings();
                Debug.Log("[AllIn1 Prefab] Settings saved.");
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(12);

        // --- Apply / Revert Buttons ---
        GUI.enabled = targetPrefabs.Count > 0;

        if (GUILayout.Button("Apply to New Only", GUILayout.Height(30)))
        {
            SaveSettings();
            ApplyToPrefabs(overwriteExisting: false);
        }

        EditorGUILayout.Space(4);

        GUI.backgroundColor = new Color(1f, 0.95f, 0.7f);
        if (GUILayout.Button("Update All Settings", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog(
                "Update All Settings",
                "이미 AllIn1 쉐이더가 적용된 머티리얼의 설정도 모두 덮어씁니다.\n" +
                "수동으로 조정한 값이 있다면 유실됩니다.\n\n계속하시겠습니까?",
                "Update All", "Cancel"))
            {
                SaveSettings();
                ApplyToPrefabs(overwriteExisting: true);
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);

        GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
        if (GUILayout.Button("Revert to URP Lit", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog(
                "Revert Shader",
                "대상 프리팹 내부의 AllIn1 머티리얼을 URP Lit으로 되돌립니다.\n" +
                "텍스처와 색상은 자동으로 복원됩니다.\n\n계속하시겠습니까?",
                "Revert", "Cancel"))
            {
                RevertPrefabsToURPLit();
            }
        }
        GUI.backgroundColor = Color.white;

        GUI.enabled = true;
    }

    void AddPrefabsFromFolder(string path, bool recursive)
    {
        if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
        {
            EditorUtility.DisplayDialog("Invalid Folder", path + " 폴더를 찾을 수 없습니다.", "OK");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { path });
        int added = 0;
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!recursive)
            {
                string dir = Path.GetDirectoryName(assetPath).Replace("\\", "/");
                if (dir != path) continue;
            }
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (go == null) continue;
            if (!targetPrefabs.Contains(go))
            {
                targetPrefabs.Add(go);
                added++;
            }
        }
        Debug.Log("[AllIn1 Prefab] Added " + added + " prefab(s) from " + path);
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
                    if (go == null) continue;
                    string p = AssetDatabase.GetAssetPath(go);
                    if (string.IsNullOrEmpty(p)) continue; // 씬 오브젝트 무시
                    if (PrefabUtility.GetPrefabAssetType(go) == PrefabAssetType.NotAPrefab) continue;
                    if (!targetPrefabs.Contains(go)) targetPrefabs.Add(go);
                }
                evt.Use();
                break;
        }
    }

    void ResetToDefaults()
    {
        lightModel = LightModelType.Toon;
        toonCutoff = 0.433f; toonSmoothness = 0f;
        shadingModel = ShadingModelType.PBR;
        metallic = 0f; smoothness = 0.7f;
        specularModel = SpecularModelType.Toon;
        specularAtten = 0f; specularToonCutoff = 0f; specularToonSmoothness = 0f;
        castShadows = true; receiveShadows = true;
        outlineType = OutlineTypeEnum.None;
        glitchEnabled = false;
        glitchTiling = 5f; glitchAmount = 0.5f;
        glitchOffset = new Vector3(-0.5f, 0f, 0f);
        glitchSpeed = 2.5f; glitchWorldSpace = true;
        SaveSettings();
        Debug.Log("[AllIn1 Prefab] Settings reset.");
    }

    // =====================================================
    // Apply / Revert  (prefab-asset oriented)
    // =====================================================

    void ApplyToPrefabs(bool overwriteExisting)
    {
        string shaderName = "AllIn13DShader/AllIn13DShader";
        Shader allIn1Shader = Shader.Find(shaderName);
        if (allIn1Shader == null)
        {
            EditorUtility.DisplayDialog("Error", shaderName + " not found!", "OK");
            return;
        }

        if (!AssetDatabase.IsValidFolder(SAVE_FOLDER))
            AssetDatabase.CreateFolder("Assets", "AllIn1_GeneratedMaterials");

        int totalPrefabs = 0, changedPrefabs = 0, skippedVariants = 0;
        int newCount = 0, updatedCount = 0, skippedCount = 0, rendererCount = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int pi = 0; pi < targetPrefabs.Count; pi++)
            {
                GameObject prefab = targetPrefabs[pi];
                if (prefab == null) continue;

                string prefabPath = AssetDatabase.GetAssetPath(prefab);
                if (string.IsNullOrEmpty(prefabPath)) continue;

                totalPrefabs++;

                if (skipVariants && PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Variant)
                {
                    skippedVariants++;
                    continue;
                }

                EditorUtility.DisplayProgressBar("AllIn1 Prefab Apply",
                    prefab.name, (float)pi / Mathf.Max(1, targetPrefabs.Count));

                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                bool prefabChanged = false;

                try
                {
                    Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                    foreach (Renderer renderer in renderers)
                    {
                        rendererCount++;
                        Material[] materials = renderer.sharedMaterials;
                        bool rChanged = false;
                        string childPath = GetChildPath(root.transform, renderer.transform);

                        for (int i = 0; i < materials.Length; i++)
                        {
                            if (materials[i] == null)
                            {
                                Material existing = FindExistingGeneratedMaterial(prefab.name, childPath, i);
                                if (existing != null)
                                {
                                    existing.shader = allIn1Shader;
                                    ApplySettings(existing);
                                    EditorUtility.SetDirty(existing);
                                    materials[i] = existing;
                                    updatedCount++;
                                }
                                else
                                {
                                    Material newMat = new Material(allIn1Shader);
                                    newMat.name = "MAT_AllIn1_" + prefab.name;
                                    ApplySettings(newMat);
                                    SaveMaterialAsset(newMat, prefab.name, childPath, i);
                                    materials[i] = newMat;
                                    newCount++;
                                }
                                rChanged = true;
                                continue;
                            }

                            string currentShader = materials[i].shader.name;
                            bool isAlreadyAllIn1 = currentShader.Contains("AllIn13DShader");

                            if (isAlreadyAllIn1)
                            {
                                if (!overwriteExisting) { skippedCount++; continue; }

                                if (materials[i].shader != allIn1Shader)
                                    materials[i].shader = allIn1Shader;
                                ApplySettings(materials[i]);

                                string existingPath = AssetDatabase.GetAssetPath(materials[i]);
                                if (string.IsNullOrEmpty(existingPath))
                                    SaveMaterialAsset(materials[i], prefab.name, childPath, i);
                                else
                                    EditorUtility.SetDirty(materials[i]);

                                updatedCount++;
                                rChanged = true;
                            }
                            else
                            {
                                Material existing = FindExistingGeneratedMaterial(prefab.name, childPath, i);
                                Material target;

                                if (existing != null)
                                {
                                    target = existing;
                                    target.shader = allIn1Shader;
                                }
                                else
                                {
                                    target = new Material(allIn1Shader);
                                    target.name = "MAT_AllIn1_" + prefab.name;
                                }

                                // 텍스처/컬러 이관
                                if (materials[i].HasProperty("_BaseMap"))
                                    target.SetTexture("_MainTex", materials[i].GetTexture("_BaseMap"));
                                else if (materials[i].HasProperty("_MainTex"))
                                    target.SetTexture("_MainTex", materials[i].GetTexture("_MainTex"));

                                if (materials[i].HasProperty("_BaseColor"))
                                    target.SetColor("_Color", materials[i].GetColor("_BaseColor"));
                                else if (materials[i].HasProperty("_Color"))
                                    target.SetColor("_Color", materials[i].GetColor("_Color"));

                                ApplySettings(target);

                                if (existing != null)
                                {
                                    EditorUtility.SetDirty(target);
                                    updatedCount++;
                                }
                                else
                                {
                                    SaveMaterialAsset(target, prefab.name, childPath, i);
                                    newCount++;
                                }

                                materials[i] = target;
                                rChanged = true;
                            }
                        }

                        if (rChanged)
                        {
                            renderer.sharedMaterials = materials;
                            prefabChanged = true;
                        }
                    }

                    if (prefabChanged)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                        changedPrefabs++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg =
            "Prefabs processed: " + totalPrefabs +
            "\nPrefabs changed:   " + changedPrefabs +
            "\nVariants skipped:  " + skippedVariants +
            "\n\nRenderers:  " + rendererCount +
            "\nNew mats:   " + newCount +
            "\nUpdated:    " + updatedCount +
            "\nSkipped:    " + skippedCount;
        Debug.Log("[AllIn1 Prefab] Done -\n" + msg);
        EditorUtility.DisplayDialog("Result", msg, "OK");
    }

    void RevertPrefabsToURPLit()
    {
        Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLitShader == null)
        {
            EditorUtility.DisplayDialog("Error", "URP Lit shader not found!", "OK");
            return;
        }

        int totalPrefabs = 0, changedPrefabs = 0, skippedVariants = 0;
        int revertedCount = 0, skippedCount = 0, rendererCount = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int pi = 0; pi < targetPrefabs.Count; pi++)
            {
                GameObject prefab = targetPrefabs[pi];
                if (prefab == null) continue;

                string prefabPath = AssetDatabase.GetAssetPath(prefab);
                if (string.IsNullOrEmpty(prefabPath)) continue;

                totalPrefabs++;

                if (skipVariants && PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Variant)
                {
                    skippedVariants++;
                    continue;
                }

                EditorUtility.DisplayProgressBar("AllIn1 Prefab Revert",
                    prefab.name, (float)pi / Mathf.Max(1, targetPrefabs.Count));

                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                bool prefabChanged = false;

                try
                {
                    Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                    foreach (Renderer renderer in renderers)
                    {
                        rendererCount++;
                        Material[] materials = renderer.sharedMaterials;
                        bool rChanged = false;

                        for (int i = 0; i < materials.Length; i++)
                        {
                            if (materials[i] == null) continue;

                            if (!materials[i].shader.name.Contains("AllIn13DShader"))
                            {
                                skippedCount++;
                                continue;
                            }

                            Texture mainTex = materials[i].HasProperty("_MainTex") ? materials[i].GetTexture("_MainTex") : null;
                            Color color = materials[i].HasProperty("_Color") ? materials[i].GetColor("_Color") : Color.white;

                            materials[i].shader = urpLitShader;
                            if (mainTex != null) materials[i].SetTexture("_BaseMap", mainTex);
                            materials[i].SetColor("_BaseColor", color);
                            materials[i].SetFloat("_Smoothness", 0.5f);
                            materials[i].SetFloat("_Metallic", 0f);

                            EditorUtility.SetDirty(materials[i]);

                            revertedCount++;
                            rChanged = true;
                        }

                        if (rChanged)
                        {
                            renderer.sharedMaterials = materials;
                            prefabChanged = true;
                        }
                    }

                    if (prefabChanged)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                        changedPrefabs++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg =
            "Prefabs processed: " + totalPrefabs +
            "\nPrefabs changed:   " + changedPrefabs +
            "\nVariants skipped:  " + skippedVariants +
            "\n\nRenderers:   " + rendererCount +
            "\nReverted:    " + revertedCount +
            "\nSkipped:     " + skippedCount;
        Debug.Log("[AllIn1 Prefab] Revert Done -\n" + msg);
        EditorUtility.DisplayDialog("Revert Result", msg, "OK");
    }

    // =====================================================
    // Helpers
    // =====================================================

    static string GetChildPath(Transform root, Transform target)
    {
        if (root == target) return "_root";
        List<string> names = new List<string>();
        Transform t = target;
        while (t != null && t != root)
        {
            names.Add(t.name);
            t = t.parent;
        }
        names.Reverse();
        return string.Join("_", names);
    }

    static string GetMaterialPath(string prefabName, string childPath, int index)
    {
        string safePrefab = Sanitize(prefabName);
        string safeChild  = Sanitize(childPath);
        return SAVE_FOLDER + "/MAT_AllIn1_" + safePrefab + "__" + safeChild + "_" + index + ".mat";
    }

    static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "_";
        return s.Replace("/", "_").Replace("\\", "_").Replace(" ", "_")
                .Replace(":", "_").Replace("*", "_").Replace("?", "_")
                .Replace("\"", "_").Replace("<", "_").Replace(">", "_").Replace("|", "_");
    }

    static Material FindExistingGeneratedMaterial(string prefabName, string childPath, int index)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(GetMaterialPath(prefabName, childPath, index));
    }

    static void SaveMaterialAsset(Material mat, string prefabName, string childPath, int index)
    {
        string path = GetMaterialPath(prefabName, childPath, index);
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mat, existing);
            EditorUtility.SetDirty(existing);
            return;
        }
        AssetDatabase.CreateAsset(mat, path);
    }

    void ApplySettings(Material mat)
    {
        // --- Light Model ---
        string[] lightKeywords = {
            "_LIGHTMODEL_NONE", "_LIGHTMODEL_CLASSIC", "_LIGHTMODEL_TOON",
            "_LIGHTMODEL_TOONRAMP", "_LIGHTMODEL_HALFLAMBERT",
            "_LIGHTMODEL_FAKEGI", "_LIGHTMODEL_FASTLIGHTING"
        };
        foreach (string kw in lightKeywords) mat.DisableKeyword(kw);

        mat.SetFloat("_LightModel", (float)lightModel);
        switch (lightModel)
        {
            case LightModelType.None:         mat.EnableKeyword("_LIGHTMODEL_NONE"); break;
            case LightModelType.Classic:      mat.EnableKeyword("_LIGHTMODEL_CLASSIC"); break;
            case LightModelType.Toon:         mat.EnableKeyword("_LIGHTMODEL_TOON"); break;
            case LightModelType.ToonRamp:     mat.EnableKeyword("_LIGHTMODEL_TOONRAMP"); break;
            case LightModelType.HalfLambert:  mat.EnableKeyword("_LIGHTMODEL_HALFLAMBERT"); break;
            case LightModelType.FakeGI:       mat.EnableKeyword("_LIGHTMODEL_FAKEGI"); break;
            case LightModelType.FastLighting: mat.EnableKeyword("_LIGHTMODEL_FASTLIGHTING"); break;
        }
        mat.SetFloat("_ToonCutoff", toonCutoff);
        mat.SetFloat("_ToonSmoothness", toonSmoothness);

        // --- Shading Model ---
        mat.DisableKeyword("_SHADINGMODEL_BASIC");
        mat.DisableKeyword("_SHADINGMODEL_PBR");
        mat.SetFloat("_ShadingModel", (float)shadingModel);
        switch (shadingModel)
        {
            case ShadingModelType.Basic: mat.EnableKeyword("_SHADINGMODEL_BASIC"); break;
            case ShadingModelType.PBR:   mat.EnableKeyword("_SHADINGMODEL_PBR"); break;
        }
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);

        // --- Specular Model ---
        string[] specKeywords = {
            "_SPECULARMODEL_NONE", "_SPECULARMODEL_CLASSIC", "_SPECULARMODEL_TOON",
            "_SPECULARMODEL_ANISOTROPIC", "_SPECULARMODEL_ANISOTROPICTOON"
        };
        foreach (string kw in specKeywords) mat.DisableKeyword(kw);

        mat.SetFloat("_SpecularModel", (float)specularModel);
        switch (specularModel)
        {
            case SpecularModelType.None:            mat.EnableKeyword("_SPECULARMODEL_NONE"); break;
            case SpecularModelType.Classic:         mat.EnableKeyword("_SPECULARMODEL_CLASSIC"); break;
            case SpecularModelType.Toon:            mat.EnableKeyword("_SPECULARMODEL_TOON"); break;
            case SpecularModelType.Anisotropic:     mat.EnableKeyword("_SPECULARMODEL_ANISOTROPIC"); break;
            case SpecularModelType.AnisotropicToon: mat.EnableKeyword("_SPECULARMODEL_ANISOTROPICTOON"); break;
        }
        mat.SetFloat("_SpecularAtten", specularAtten);
        mat.SetFloat("_SpecularToonCutoff", specularToonCutoff);
        mat.SetFloat("_SpecularToonSmoothness", specularToonSmoothness);

        // --- Shadows ---
        mat.SetFloat("_CastShadowsOn", castShadows ? 1f : 0f);
        if (castShadows) mat.EnableKeyword("_CAST_SHADOWS_ON");
        else mat.DisableKeyword("_CAST_SHADOWS_ON");

        mat.SetFloat("_ReceiveShadows", receiveShadows ? 1f : 0f);
        if (receiveShadows) mat.EnableKeyword("_RECEIVE_SHADOWS_ON");
        else mat.DisableKeyword("_RECEIVE_SHADOWS_ON");

        // --- Outline ---
        string[] outlineKeywords = {
            "_OUTLINETYPE_NONE", "_OUTLINETYPE_CONSTANT",
            "_OUTLINETYPE_SIMPLE", "_OUTLINETYPE_FADEWITHDISTANCE"
        };
        foreach (string kw in outlineKeywords) mat.DisableKeyword(kw);

        mat.SetFloat("_OutlineType", (float)outlineType);
        switch (outlineType)
        {
            case OutlineTypeEnum.None:             mat.EnableKeyword("_OUTLINETYPE_NONE"); break;
            case OutlineTypeEnum.Constant:         mat.EnableKeyword("_OUTLINETYPE_CONSTANT"); break;
            case OutlineTypeEnum.Simple:           mat.EnableKeyword("_OUTLINETYPE_SIMPLE"); break;
            case OutlineTypeEnum.FadeWithDistance: mat.EnableKeyword("_OUTLINETYPE_FADEWITHDISTANCE"); break;
        }

        // --- Glitch Effect ---
        if (glitchEnabled)
        {
            mat.EnableKeyword("_GLITCH_ON");
            mat.SetFloat("_Glitch", 1f);
            mat.SetFloat("_GlitchTiling", glitchTiling);
            mat.SetFloat("_GlitchAmount", glitchAmount);
            mat.SetVector("_GlitchOffset", new Vector4(glitchOffset.x, glitchOffset.y, glitchOffset.z, 0f));
            mat.SetFloat("_GlitchSpeed", glitchSpeed);
            mat.SetFloat("_GlitchWorldSpace", glitchWorldSpace ? 1f : 0f);
        }
        else
        {
            mat.DisableKeyword("_GLITCH_ON");
            mat.SetFloat("_Glitch", 0f);
        }
    }
}
