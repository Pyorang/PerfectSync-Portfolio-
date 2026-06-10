using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 캐릭터 프리팹의 모든 치장요소(파츠)를 자동으로 촬영하여 PNG 썸네일을 생성하는 Editor 도구.
/// - 전체 계층 구조를 재귀 탐색하여 본(bone) 깊숙이 있는 파츠도 모두 찾음
/// - 24개 카메라 앵글에서 모두 촬영하여 저장
/// - 2-패스 렌더링으로 URP/HDRP에서도 완벽한 투명 배경 지원
/// 사용법: Unity 메뉴 → Tools → Generate Part Thumbnails
/// </summary>
public class PartThumbnailGenerator : EditorWindow
{
    // ── 설정 ──
    private string prefabFolderPath = "Assets/PartyMonsterRumblePBR/Prefab";
    private string outputRootPath = "Assets/PartyMonsterRumblePBR/Thumbnails";
    private int thumbnailSize = 256;
    private Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0f);
    private float cameraDistanceMultiplier = 2.5f;
    private bool transparentBackground = true;

    // ── 파츠 이름 패턴 (접두사 + 숫자) ──
    private static readonly string[] AllPartPrefixes = new string[]
    {
        "Bodypart", "MainBody", "Eye", "Glove", "Mouth", "Nose",
        "Tail", "Ear", "Hat", "Horn", "Hair", "Comb", "Grass"
    };

    // 컨테이너 이름들 — 파츠가 아니므로 건너뜀
    private static readonly HashSet<string> ContainerNames = new HashSet<string>
    {
        "Bodies", "Bodyparts", "Eyes", "Gloves", "Headparts",
        "MouthandNoses", "Tails", "Hats", "Horns"
    };

    // ── 24개 카메라 앵글 (elevation, azimuth) ──
    // 각 파츠마다 이 24개 앵글 전부에서 촬영
    private static readonly Vector2[] AllAngles = new Vector2[]
    {
        // 수평 회전 (elevation 0~5도) — 정면~뒤 8방향
        new Vector2(  0f,    0f),   // 00_Front
        new Vector2(  0f,   45f),   // 01_FrontLeft45
        new Vector2(  0f,   90f),   // 02_Left
        new Vector2(  0f,  135f),   // 03_BackLeft135
        new Vector2(  0f,  180f),   // 04_Back
        new Vector2(  0f, -135f),   // 05_BackRight135
        new Vector2(  0f,  -90f),   // 06_Right
        new Vector2(  0f,  -45f),   // 07_FrontRight45

        // 살짝 위에서 (elevation 25도) — 8방향
        new Vector2( 25f,    0f),   // 08_Hi25_Front
        new Vector2( 25f,   45f),   // 09_Hi25_FrontLeft45
        new Vector2( 25f,   90f),   // 10_Hi25_Left
        new Vector2( 25f,  135f),   // 11_Hi25_BackLeft135
        new Vector2( 25f,  180f),   // 12_Hi25_Back
        new Vector2( 25f, -135f),   // 13_Hi25_BackRight135
        new Vector2( 25f,  -90f),   // 14_Hi25_Right
        new Vector2( 25f,  -45f),   // 15_Hi25_FrontRight45

        // 높은 위에서 (elevation 50도) — 4방향
        new Vector2( 50f,    0f),   // 16_Hi50_Front
        new Vector2( 50f,   90f),   // 17_Hi50_Left
        new Vector2( 50f,  180f),   // 18_Hi50_Back
        new Vector2( 50f,  -90f),   // 19_Hi50_Right

        // 약간 아래에서 올려다봄 (elevation -15도) — 4방향
        new Vector2(-15f,    0f),   // 20_Lo_Front
        new Vector2(-15f,   90f),   // 21_Lo_Left
        new Vector2(-15f,  180f),   // 22_Lo_Back
        new Vector2(-15f,  -90f),   // 23_Lo_Right
    };

    // 앵글 이름 (파일명에 사용)
    private static readonly string[] AngleNames = new string[]
    {
        "00_Front",
        "01_FrontLeft45",
        "02_Left",
        "03_BackLeft135",
        "04_Back",
        "05_BackRight135",
        "06_Right",
        "07_FrontRight45",

        "08_Hi25_Front",
        "09_Hi25_FrontLeft45",
        "10_Hi25_Left",
        "11_Hi25_BackLeft135",
        "12_Hi25_Back",
        "13_Hi25_BackRight135",
        "14_Hi25_Right",
        "15_Hi25_FrontRight45",

        "16_Hi50_Front",
        "17_Hi50_Left",
        "18_Hi50_Back",
        "19_Hi50_Right",

        "20_Lo_Front",
        "21_Lo_Left",
        "22_Lo_Back",
        "23_Lo_Right",
    };

    private Vector2 scrollPos;
    private bool isGenerating = false;
    private int totalParts = 0;
    private int processedParts = 0;

    [MenuItem("Tools/Generate Part Thumbnails")]
    public static void ShowWindow()
    {
        var window = GetWindow<PartThumbnailGenerator>("Part Thumbnail Generator");
        window.minSize = new Vector2(400, 380);
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("파츠 썸네일 자동 생성기 (24앵글)", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        prefabFolderPath = EditorGUILayout.TextField("프리팹 폴더 경로", prefabFolderPath);
        outputRootPath = EditorGUILayout.TextField("출력 폴더 경로", outputRootPath);
        thumbnailSize = EditorGUILayout.IntSlider("썸네일 크기 (px)", thumbnailSize, 64, 1024);
        cameraDistanceMultiplier = EditorGUILayout.Slider("카메라 거리 배수", cameraDistanceMultiplier, 1.5f, 5f);
        transparentBackground = EditorGUILayout.Toggle("투명 배경", transparentBackground);

        if (!transparentBackground)
        {
            backgroundColor = EditorGUILayout.ColorField("배경색", backgroundColor);
        }

        EditorGUILayout.Space(10);

        if (isGenerating)
        {
            float progress = totalParts > 0 ? (float)processedParts / totalParts : 0f;
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20), progress,
                $"생성 중... {processedParts}/{totalParts}");

            if (GUILayout.Button("중지"))
            {
                isGenerating = false;
            }
        }
        else
        {
            if (GUILayout.Button("선택한 프리팹 썸네일 생성 (24앵글)", GUILayout.Height(40)))
            {
                GenerateForSelectedPrefab();
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button("전체 프리팹 썸네일 생성 (C01~C30)", GUILayout.Height(30)))
            {
                GenerateAllThumbnails();
            }
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "파츠 하나당 24개 앵글에서 모두 촬영합니다.\n\n" +
            "출력 구조:\n" +
            "  {출력폴더}/{카테고리}/{파츠명}/{앵글이름}.png\n" +
            "  예: Thumbnails/Eyes/Eye01/00_Front.png\n" +
            "       Thumbnails/Eyes/Eye01/08_Hi25_Front.png\n\n" +
            "※ 모든 프리팹이 동일한 메시/머티리얼을 공유하므로\n" +
            "   하나의 프리팹만 찍어도 충분합니다.\n\n" +
            "앵글 구성: 수평8 + 25도위8 + 50도위4 + 아래4 = 24개",
            MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    // ──────────────────────────────────────────
    // 파츠 탐색
    // ──────────────────────────────────────────

    private List<(string partName, string category, Transform transform)> CollectAllParts(Transform root)
    {
        var results = new List<(string, string, Transform)>();
        var seenNames = new HashSet<string>();
        CollectAllPartsRecursive(root, results, seenNames);
        return results;
    }

    private void CollectAllPartsRecursive(Transform current,
        List<(string, string, Transform)> results, HashSet<string> seenNames)
    {
        string name = current.name;

        if (!ContainerNames.Contains(name))
        {
            foreach (string prefix in AllPartPrefixes)
            {
                if (name.StartsWith(prefix) && name.Length > prefix.Length
                    && char.IsDigit(name[prefix.Length]))
                {
                    if (!seenNames.Contains(name))
                    {
                        seenNames.Add(name);
                        string category = DetermineCategory(name);
                        results.Add((name, category, current));
                    }
                    break;
                }
            }
        }

        for (int i = 0; i < current.childCount; i++)
        {
            CollectAllPartsRecursive(current.GetChild(i), results, seenNames);
        }
    }

    // ──────────────────────────────────────────
    // 생성 로직
    // ──────────────────────────────────────────

    private void GenerateAllThumbnails()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolderPath });

        List<string> prefabPaths = new List<string>();
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(path);
            if (fileName.StartsWith("C") && fileName.Length <= 3)
                prefabPaths.Add(path);
        }
        prefabPaths.Sort();

        if (prefabPaths.Count == 0)
        {
            EditorUtility.DisplayDialog("오류", $"'{prefabFolderPath}'에서 캐릭터 프리팹을 찾을 수 없습니다.", "확인");
            return;
        }

        // 81 파츠 × 24 앵글 × 프리팹 수
        totalParts = prefabPaths.Count * 81 * AllAngles.Length;
        processedParts = 0;
        isGenerating = true;

        try
        {
            foreach (string prefabPath in prefabPaths)
            {
                if (!isGenerating) break;
                string charName = Path.GetFileNameWithoutExtension(prefabPath);
                EditorUtility.DisplayProgressBar("썸네일 생성 중",
                    $"{charName} 처리 중...",
                    (float)processedParts / totalParts);
                GenerateThumbnailsForPrefab(prefabPath);
            }
        }
        finally
        {
            isGenerating = false;
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
            Debug.Log($"[ThumbnailGenerator] 완료! 총 {processedParts}개 이미지 생성됨. 출력 경로: {outputRootPath}");
        }
    }

    private void GenerateForSelectedPrefab()
    {
        GameObject selectedObj = Selection.activeObject as GameObject;
        if (selectedObj == null)
        {
            EditorUtility.DisplayDialog("오류", "Project 창에서 프리팹을 선택해주세요.", "확인");
            return;
        }

        string path = AssetDatabase.GetAssetPath(selectedObj);
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
        {
            EditorUtility.DisplayDialog("오류", "선택한 오브젝트가 프리팹이 아닙니다.", "확인");
            return;
        }

        totalParts = 81 * AllAngles.Length; // 81파츠 × 24앵글
        processedParts = 0;
        isGenerating = true;

        try
        {
            GenerateThumbnailsForPrefab(path);
        }
        finally
        {
            isGenerating = false;
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
            Debug.Log($"[ThumbnailGenerator] 완료! {processedParts}개 이미지 생성됨.");
        }
    }

    private void GenerateThumbnailsForPrefab(string prefabPath)
    {
        string charName = Path.GetFileNameWithoutExtension(prefabPath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[ThumbnailGenerator] 프리팹 로드 실패: {prefabPath}");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;

        Renderer[] allRenderers = instance.GetComponentsInChildren<Renderer>(true);

        try
        {
            var allParts = CollectAllParts(instance.transform);
            Debug.Log($"[ThumbnailGenerator] {charName}: {allParts.Count}개 파츠 × {AllAngles.Length}개 앵글 = {allParts.Count * AllAngles.Length}장 촬영 시작");

            foreach (var (partName, category, partTransform) in allParts)
            {
                if (!isGenerating) return;

                // 출력 폴더: Thumbnails/{카테고리}/{파츠명}/
                string partOutputDir = Path.Combine(outputRootPath, category, partName);

                CapturePartAllAngles(instance, allRenderers, partTransform, partOutputDir, partName);
            }
        }
        finally
        {
            DestroyImmediate(instance);
        }
    }

    // ──────────────────────────────────────────
    // 핵심: 파츠 하나를 24개 앵글에서 모두 촬영
    // ──────────────────────────────────────────

    private void CapturePartAllAngles(GameObject instance, Renderer[] allRenderers,
        Transform targetPart, string partOutputDir, string partName)
    {
        // 모든 렌더러 끄기
        foreach (var r in allRenderers)
            r.enabled = false;

        // 대상 파츠 렌더러만 켜기
        Renderer[] partRenderers = targetPart.GetComponentsInChildren<Renderer>(true);
        if (partRenderers.Length == 0)
        {
            Debug.LogWarning($"[ThumbnailGenerator] '{partName}'에 Renderer가 없음, 건너뜀");
            processedParts += AllAngles.Length; // 스킵한 만큼 카운트 진행
            return;
        }

        EnsureActive(targetPart);
        foreach (var r in partRenderers)
        {
            r.enabled = true;
            r.gameObject.SetActive(true);
        }

        // 바운드 계산
        Bounds bounds = CalculateBounds(partRenderers);
        if (bounds.size == Vector3.zero)
        {
            Debug.LogWarning($"[ThumbnailGenerator] '{partName}' 바운드 크기 0, 건너뜀");
            processedParts += AllAngles.Length;
            return;
        }

        // 출력 폴더 생성
        string absoluteDir = Path.Combine(Application.dataPath, "..", partOutputDir);
        if (!Directory.Exists(absoluteDir))
            Directory.CreateDirectory(absoluteDir);

        // 임시 카메라 + 라이트 (한 번만 생성, 24번 재사용)
        RenderTexture rt = new RenderTexture(thumbnailSize, thumbnailSize, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;

        GameObject camObj = new GameObject("__ThumbnailCam__");
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.orthographic = false;
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 100f;
        cam.targetTexture = rt;
        cam.cullingMask = ~0;
        cam.allowHDR = false;
        cam.allowMSAA = true;

        GameObject lightObj = new GameObject("__ThumbnailLight__");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.color = Color.white;

        GameObject fillLightObj = new GameObject("__ThumbnailFillLight__");
        Light fillLight = fillLightObj.AddComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.intensity = 0.4f;
        fillLight.color = new Color(0.8f, 0.85f, 1f);

        // ── 24개 앵글 순회하며 전부 촬영 ──
        for (int i = 0; i < AllAngles.Length; i++)
        {
            if (!isGenerating) break;

            Vector2 angle = AllAngles[i];
            string angleName = AngleNames[i];

            // 카메라 배치
            PositionCamera(camObj.transform, bounds, angle, cam.fieldOfView);

            // 라이트를 카메라 방향 기준으로 배치
            lightObj.transform.rotation = Quaternion.Euler(angle.x + 25f, angle.y, 0f);
            fillLightObj.transform.rotation = Quaternion.Euler(angle.x + 10f, angle.y + 180f, 0f);

            // 렌더링 + 저장
            Texture2D tex;
            if (transparentBackground)
            {
                tex = RenderWithTransparency(cam, rt);
            }
            else
            {
                cam.backgroundColor = backgroundColor;
                cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(thumbnailSize, thumbnailSize, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, thumbnailSize, thumbnailSize), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
            }

            string filePath = Path.Combine(absoluteDir, angleName + ".png");
            File.WriteAllBytes(filePath, tex.EncodeToPNG());
            DestroyImmediate(tex);

            processedParts++;

            // 프로그레스 바 업데이트
            if (processedParts % 24 == 0) // 파츠 하나 완료할 때마다 업데이트
            {
                EditorUtility.DisplayProgressBar("썸네일 생성 중",
                    $"{partName} 촬영 완료 ({processedParts}/{totalParts})",
                    (float)processedParts / totalParts);
            }
        }

        // 정리
        cam.targetTexture = null;
        DestroyImmediate(camObj);
        DestroyImmediate(lightObj);
        DestroyImmediate(fillLightObj);
        rt.Release();
        DestroyImmediate(rt);
    }

    // ──────────────────────────────────────────
    // 2-패스 투명 배경 렌더링
    // ──────────────────────────────────────────

    private Texture2D RenderWithTransparency(Camera cam, RenderTexture rt)
    {
        int w = rt.width;
        int h = rt.height;

        cam.backgroundColor = Color.black;
        cam.Render();
        RenderTexture.active = rt;
        Texture2D texBlack = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texBlack.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        texBlack.Apply();
        RenderTexture.active = null;

        cam.backgroundColor = Color.white;
        cam.Render();
        RenderTexture.active = rt;
        Texture2D texWhite = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texWhite.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        texWhite.Apply();
        RenderTexture.active = null;

        Color32[] blackPixels = texBlack.GetPixels32();
        Color32[] whitePixels = texWhite.GetPixels32();
        Color32[] result = new Color32[blackPixels.Length];

        for (int i = 0; i < blackPixels.Length; i++)
        {
            float diffR = (whitePixels[i].r - blackPixels[i].r) / 255f;
            float diffG = (whitePixels[i].g - blackPixels[i].g) / 255f;
            float diffB = (whitePixels[i].b - blackPixels[i].b) / 255f;

            float alpha = 1f - (diffR + diffG + diffB) / 3f;
            alpha = Mathf.Clamp01(alpha);

            if (alpha < 0.004f)
            {
                result[i] = new Color32(0, 0, 0, 0);
            }
            else
            {
                byte r2 = (byte)Mathf.Clamp(blackPixels[i].r / alpha, 0, 255);
                byte g = (byte)Mathf.Clamp(blackPixels[i].g / alpha, 0, 255);
                byte b = (byte)Mathf.Clamp(blackPixels[i].b / alpha, 0, 255);
                byte a = (byte)Mathf.Clamp(alpha * 255f, 0, 255);
                result[i] = new Color32(r2, g, b, a);
            }
        }

        DestroyImmediate(texBlack);
        DestroyImmediate(texWhite);

        Texture2D finalTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        finalTex.SetPixels32(result);
        finalTex.Apply();
        return finalTex;
    }

    // ──────────────────────────────────────────
    // 카메라 배치
    // ──────────────────────────────────────────

    private void PositionCamera(Transform camTransform, Bounds bounds, Vector2 angle, float fov)
    {
        float elevation = angle.x;
        float azimuth = angle.y;

        float maxExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        float distance = maxExtent * cameraDistanceMultiplier / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

        Vector3 dir = Quaternion.Euler(-elevation, azimuth, 0f) * Vector3.forward;
        camTransform.position = bounds.center - dir * distance;
        camTransform.LookAt(bounds.center);
    }

    // ──────────────────────────────────────────
    // 유틸리티
    // ──────────────────────────────────────────

    private string DetermineCategory(string partName)
    {
        if (partName.StartsWith("Bodypart")) return "Bodyparts";
        if (partName.StartsWith("MainBody")) return "Bodies";
        if (partName.StartsWith("Eye")) return "Eyes";
        if (partName.StartsWith("Glove")) return "Gloves";
        if (partName.StartsWith("Mouth")) return "Mouths";
        if (partName.StartsWith("Nose")) return "Noses";
        if (partName.StartsWith("Tail")) return "Tails";
        if (partName.StartsWith("Ear")) return "Ears";
        if (partName.StartsWith("Hat")) return "Hats";
        if (partName.StartsWith("Horn")) return "Horns";
        if (partName.StartsWith("Hair")) return "Hairs";
        if (partName.StartsWith("Comb")) return "Combs";
        if (partName.StartsWith("Grass")) return "Grass";
        return "Misc";
    }

    private Bounds CalculateBounds(Renderer[] renderers)
    {
        Bounds bounds = new Bounds();
        bool first = true;
        foreach (var r in renderers)
        {
            if (!r.enabled) continue;
            if (first)
            {
                bounds = r.bounds;
                first = false;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }
        return bounds;
    }

    private void EnsureActive(Transform t)
    {
        List<Transform> chain = new List<Transform>();
        Transform current = t;
        while (current != null)
        {
            chain.Add(current);
            current = current.parent;
        }
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            chain[i].gameObject.SetActive(true);
        }
    }
}
