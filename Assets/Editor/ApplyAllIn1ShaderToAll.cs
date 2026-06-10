using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class ApplyAllIn1ShaderToAll : EditorWindow
{
    static string SAVE_FOLDER = "Assets/AllIn1_GeneratedMaterials";

    List<GameObject> targetObjects = new List<GameObject>();
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

    // EditorPrefs keys
    const string PREF_PREFIX = "AllIn1ShaderApplier_";

    [MenuItem("Tools/All In 1 - Apply Shader")]
    static void ShowWindow()
    {
        var window = GetWindow<ApplyAllIn1ShaderToAll>("AllIn1 Shader Applier");
        window.minSize = new Vector2(380, 500);
        window.Show();
    }

    void OnEnable()
    {
        LoadSettings();
    }

    void OnDisable()
    {
        SaveSettings();
    }

    void LoadSettings()
    {
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
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.MinHeight(60), GUILayout.MaxHeight(200));

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

        EditorGUILayout.Space(8);

        // ========================
        // --- Settings Panel ---
        // ========================
        showSettings = EditorGUILayout.Foldout(showSettings, "Shader Settings", true, EditorStyles.foldoutHeader);

        if (showSettings)
        {
            EditorGUI.indentLevel++;

            // --- Light Model ---
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Light Model", EditorStyles.boldLabel);
            lightModel = (LightModelType)EditorGUILayout.EnumPopup("Type", lightModel);
            if (lightModel == LightModelType.Toon)
            {
                toonCutoff = EditorGUILayout.Slider("Toon Cutoff", toonCutoff, 0f, 1f);
                toonSmoothness = EditorGUILayout.Slider("Toon Smoothness", toonSmoothness, 0f, 1f);
            }

            // --- Shading Model ---
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Shading Model", EditorStyles.boldLabel);
            shadingModel = (ShadingModelType)EditorGUILayout.EnumPopup("Type", shadingModel);
            if (shadingModel == ShadingModelType.PBR)
            {
                metallic = EditorGUILayout.Slider("Metallic", metallic, 0f, 1f);
                smoothness = EditorGUILayout.Slider("Smoothness", smoothness, 0f, 1f);
            }

            // --- Specular Model ---
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Specular Model", EditorStyles.boldLabel);
            specularModel = (SpecularModelType)EditorGUILayout.EnumPopup("Type", specularModel);
            if (specularModel != SpecularModelType.None)
            {
                specularAtten = EditorGUILayout.Slider("Specular Atten", specularAtten, 0f, 1f);
            }
            if (specularModel == SpecularModelType.Toon)
            {
                specularToonCutoff = EditorGUILayout.Slider("Specular Toon Cutoff", specularToonCutoff, 0f, 1f);
                specularToonSmoothness = EditorGUILayout.Slider("Specular Toon Smoothness", specularToonSmoothness, 0f, 1f);
            }

            // --- Shadows ---
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Shadows", EditorStyles.boldLabel);
            castShadows = EditorGUILayout.Toggle("Cast Shadows", castShadows);
            receiveShadows = EditorGUILayout.Toggle("Receive Shadows", receiveShadows);

            // --- Outline ---
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Outline", EditorStyles.boldLabel);
            outlineType = (OutlineTypeEnum)EditorGUILayout.EnumPopup("Type", outlineType);

            // --- Glitch Effect ---
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
            if (GUILayout.Button("Reset to Defaults"))
            {
                ResetToDefaults();
            }
            if (GUILayout.Button("Save Settings"))
            {
                SaveSettings();
                Debug.Log("[AllIn1] Settings saved.");
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(12);

        // --- Apply / Revert Buttons ---
        GUI.enabled = targetObjects.Count > 0;

        // Apply to New Only - safe, won't overwrite manual tweaks
        if (GUILayout.Button("Apply to New Only", GUILayout.Height(30)))
        {
            SaveSettings();
            ApplyToTargets(overwriteExisting: false);
        }

        EditorGUILayout.Space(4);

        // Update All Settings - overwrites everything
        GUI.backgroundColor = new Color(1f, 0.95f, 0.7f);
        if (GUILayout.Button("Update All Settings", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog(
                "Update All Settings",
                "이미 AllIn1 쉐이더가 적용된 머티리얼의 설정도 모두 덮어씁니다.\n수동으로 조정한 값이 있다면 유실됩니다.\n\n계속하시겠습니까?",
                "Update All", "Cancel"))
            {
                SaveSettings();
                ApplyToTargets(overwriteExisting: true);
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);

        // Revert button
        GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
        if (GUILayout.Button("Revert to URP Lit", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog(
                "Revert Shader",
                "선택한 오브젝트의 AllIn1 쉐이더를 URP Lit으로 되돌립니다.\n텍스처와 색상은 자동으로 복원됩니다.\n\n계속하시겠습니까?",
                "Revert", "Cancel"))
            {
                RevertToURPLit();
            }
        }
        GUI.backgroundColor = Color.white;

        GUI.enabled = true;
    }

    void ResetToDefaults()
    {
        lightModel = LightModelType.Toon;
        toonCutoff = 0.433f;
        toonSmoothness = 0f;

        shadingModel = ShadingModelType.PBR;
        metallic = 0f;
        smoothness = 0.7f;

        specularModel = SpecularModelType.Toon;
        specularAtten = 0f;
        specularToonCutoff = 0f;
        specularToonSmoothness = 0f;

        castShadows = true;
        receiveShadows = true;

        outlineType = OutlineTypeEnum.None;

        glitchEnabled = false;
        glitchTiling = 5f;
        glitchAmount = 0.5f;
        glitchOffset = new Vector3(-0.5f, 0f, 0f);
        glitchSpeed = 2.5f;
        glitchWorldSpace = true;

        SaveSettings();
        Debug.Log("[AllIn1] Settings reset to defaults.");
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

    void ApplyToTargets(bool overwriteExisting)
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
        int skippedCount = 0;

        foreach (Renderer renderer in allRenderers)
        {
            Undo.RecordObject(renderer, "Apply AllIn1 Shader");
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < materials.Length; i++)
            {
                // Handle missing (null) materials - always reassign
                if (materials[i] == null)
                {
                    Material existingGenMat = FindExistingGeneratedMaterial(renderer.gameObject.name, i);
                    if (existingGenMat != null)
                    {
                        Undo.RecordObject(existingGenMat, "Reassign AllIn1 Material");
                        existingGenMat.shader = allIn1Shader;
                        ApplySettings(existingGenMat);
                        EditorUtility.SetDirty(existingGenMat);
                        materials[i] = existingGenMat;
                        updatedCount++;
                    }
                    else
                    {
                        Material newMat = new Material(allIn1Shader);
                        newMat.name = "MAT_AllIn1_" + renderer.gameObject.name;
                        ApplySettings(newMat);
                        // IMPORTANT: use the on-disk asset reference returned by
                        // SaveMaterialAsset (not the in-memory `newMat`) so the
                        // renderer never references an orphan Material.
                        materials[i] = SaveMaterialAsset(newMat, renderer.gameObject.name, i);
                        newCount++;
                    }
                    changed = true;
                    continue;
                }

                string currentShader = materials[i].shader.name;
                bool isAlreadyAllIn1 = currentShader.Contains("AllIn13DShader");

                if (isAlreadyAllIn1)
                {
                    // Skip existing AllIn1 materials unless overwriteExisting is true
                    if (!overwriteExisting)
                    {
                        skippedCount++;
                        continue;
                    }

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
                        // Renderer was referencing an orphan AllIn1 material.
                        // Rebind to the on-disk asset so serialization stays valid.
                        materials[i] = SaveMaterialAsset(materials[i], renderer.gameObject.name, i);
                        changed = true;
                    }
                    else
                    {
                        EditorUtility.SetDirty(materials[i]);
                    }
                    updatedCount++;
                }
                else
                {
                    // Non-AllIn1 material: check if a generated material already exists
                    Material existingGenMat = FindExistingGeneratedMaterial(renderer.gameObject.name, i);

                    if (existingGenMat != null)
                    {
                        Undo.RecordObject(existingGenMat, "Update AllIn1 Generated Material");
                        existingGenMat.shader = allIn1Shader;

                        if (materials[i].HasProperty("_BaseMap"))
                            existingGenMat.SetTexture("_MainTex", materials[i].GetTexture("_BaseMap"));
                        else if (materials[i].HasProperty("_MainTex"))
                            existingGenMat.SetTexture("_MainTex", materials[i].GetTexture("_MainTex"));

                        if (materials[i].HasProperty("_BaseColor"))
                            existingGenMat.SetColor("_Color", materials[i].GetColor("_BaseColor"));
                        else if (materials[i].HasProperty("_Color"))
                            existingGenMat.SetColor("_Color", materials[i].GetColor("_Color"));

                        ApplySettings(existingGenMat);
                        EditorUtility.SetDirty(existingGenMat);

                        materials[i] = existingGenMat;
                        updatedCount++;
                        changed = true;
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
                        // Use the returned on-disk asset, never the in-memory copy.
                        materials[i] = SaveMaterialAsset(newMat, renderer.gameObject.name, i);
                        newCount++;
                        changed = true;
                    }
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
            + "\nSkipped (already applied): " + skippedCount
            + "\nTotal renderers: " + allRenderers.Count;
        Debug.Log("[AllIn1] Done - " + msg);
        EditorUtility.DisplayDialog("Result", msg, "OK");
    }

    static string GetMaterialPath(string objName, int index)
    {
        string safeName = objName.Replace("/", "_").Replace("\\", "_").Replace(" ", "_");
        return SAVE_FOLDER + "/MAT_AllIn1_" + safeName + "_" + index + ".mat";
    }

    static Material FindExistingGeneratedMaterial(string objName, int index)
    {
        string path = GetMaterialPath(objName, index);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    // Returns the on-disk Material asset that should be referenced by renderers.
    // CRITICAL: Callers MUST use the returned reference. Reusing the in-memory `mat`
    // after this call can leave renderers pointing at a GUID-less orphan Material,
    // which serializes as a missing reference (hot-pink) on other machines.
    static Material SaveMaterialAsset(Material mat, string objName, int index)
    {
        string path = GetMaterialPath(objName, index);

        // If a material already exists at this path, don't create a duplicate.
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mat, existing);
            EditorUtility.SetDirty(existing);

            // The in-memory `mat` is now an orphan duplicate. Destroy to prevent
            // accidental reuse and a memory leak.
            if (mat != existing && !AssetDatabase.Contains(mat))
                UnityEngine.Object.DestroyImmediate(mat);

            return existing;
        }

        AssetDatabase.CreateAsset(mat, path);
        // Force immediate import so the asset has a stable GUID before we hand
        // out a reference to it.
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return mat;
    }

    void RevertToURPLit()
    {
        Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLitShader == null)
        {
            EditorUtility.DisplayDialog("Error", "URP Lit shader not found!", "OK");
            return;
        }

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

        int revertedCount = 0;
        int skippedCount = 0;

        foreach (Renderer renderer in allRenderers)
        {
            Undo.RecordObject(renderer, "Revert to URP Lit");
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null) continue;

                bool isAllIn1 = materials[i].shader.name.Contains("AllIn13DShader");
                if (!isAllIn1)
                {
                    skippedCount++;
                    continue;
                }

                Undo.RecordObject(materials[i], "Revert Material to URP Lit");

                // Save properties before shader change
                Texture mainTex = null;
                Color color = Color.white;

                if (materials[i].HasProperty("_MainTex"))
                    mainTex = materials[i].GetTexture("_MainTex");
                if (materials[i].HasProperty("_Color"))
                    color = materials[i].GetColor("_Color");

                // Switch shader
                materials[i].shader = urpLitShader;

                // Restore properties to URP Lit names
                if (mainTex != null)
                    materials[i].SetTexture("_BaseMap", mainTex);
                materials[i].SetColor("_BaseColor", color);

                // Set sensible URP Lit defaults
                materials[i].SetFloat("_Smoothness", 0.5f);
                materials[i].SetFloat("_Metallic", 0f);

                string existingPath = AssetDatabase.GetAssetPath(materials[i]);
                if (!string.IsNullOrEmpty(existingPath))
                {
                    EditorUtility.SetDirty(materials[i]);
                }

                changed = true;
                revertedCount++;
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = "Reverted: " + revertedCount
            + "\nSkipped (non-AllIn1): " + skippedCount
            + "\nTotal renderers: " + allRenderers.Count;
        Debug.Log("[AllIn1] Revert Done - " + msg);
        EditorUtility.DisplayDialog("Revert Result", msg, "OK");
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
            case OutlineTypeEnum.FadeWithDistance:  mat.EnableKeyword("_OUTLINETYPE_FADEWITHDISTANCE"); break;
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
