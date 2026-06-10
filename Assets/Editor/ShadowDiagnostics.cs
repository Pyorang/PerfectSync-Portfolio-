using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ShadowDiagnostics
{
    [MenuItem("Tools/Shadow Diagnostics")]
    public static void RunDiagnostics()
    {
        Debug.Log("=== SHADOW DIAGNOSTICS START ===");
        
        // 1. Check all Lights
        var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var light in lights)
        {
            Debug.Log($"[LIGHT] Name={light.name}, Type={light.type}, Enabled={light.enabled}, " +
                      $"ShadowType={light.shadows}, Intensity={light.intensity}, " +
                      $"CullingMask={light.cullingMask}, RenderingLayerMask={light.renderingLayerMask}, " +
                      $"GameObject.active={light.gameObject.activeInHierarchy}");
            
            var urpLightData = light.GetComponent<UniversalAdditionalLightData>();
            if (urpLightData != null)
            {
                Debug.Log($"  [URP Light] usePipelineSettings={urpLightData.usePipelineSettings}");
            }
        }
        
        // 2. Check target objects
        string[] targets = { "Supermarket", "Trampoline_04" };
        foreach (var targetName in targets)
        {
            var go = GameObject.Find(targetName);
            if (go == null)
            {
                // Try finding recursively
                var allGOs = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                foreach (var g in allGOs)
                {
                    if (g.name == targetName)
                    {
                        go = g;
                        break;
                    }
                }
            }
            
            if (go == null)
            {
                Debug.LogWarning($"[TARGET] {targetName} NOT FOUND in scene!");
                continue;
            }
            
            Debug.Log($"[TARGET] {targetName}: Layer={go.layer}({LayerMask.LayerToName(go.layer)}), " +
                      $"Position={go.transform.position}, Active={go.activeInHierarchy}, " +
                      $"Static={go.isStatic}, StaticFlags={GameObjectUtility.GetStaticEditorFlags(go)}");
            
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            Debug.Log($"  Found {renderers.Length} renderers in children");
            
            int count = 0;
            foreach (var r in renderers)
            {
                if (count < 5) // limit output
                {
                    Debug.Log($"  [Renderer] {r.gameObject.name}: CastShadows={r.shadowCastingMode}, " +
                              $"ReceiveShadows={r.receiveShadows}, Enabled={r.enabled}, " +
                              $"Layer={r.gameObject.layer}({LayerMask.LayerToName(r.gameObject.layer)}), " +
                              $"Material={r.sharedMaterial?.name ?? "null"}, " +
                              $"Shader={r.sharedMaterial?.shader?.name ?? "null"}, " +
                              $"RenderingLayerMask={r.renderingLayerMask}");
                }
                count++;
            }
            if (count > 5) Debug.Log($"  ... and {count - 5} more renderers");
        }
        
        // 3. Check URP Pipeline Asset
        var pipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipelineAsset != null)
        {
            Debug.Log($"[URP] Pipeline={pipelineAsset.name}, " +
                      $"MainLightShadows={pipelineAsset.supportsMainLightShadows}, " +
                      $"ShadowDistance={pipelineAsset.shadowDistance}, " +
                      $"ShadowCascades={pipelineAsset.shadowCascadeCount}, " +
                      $"ShadowResolution={pipelineAsset.mainLightShadowmapResolution}, " +
                      $"SoftShadows={pipelineAsset.supportsSoftShadows}");
        }
        
        // 4. Check Camera distance to targets
        var cam = Camera.main;
        if (cam != null)
        {
            Debug.Log($"[CAMERA] Main camera at {cam.transform.position}");
            foreach (var targetName in targets)
            {
                var go = GameObject.Find(targetName);
                if (go == null)
                {
                    var allGOs = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                    foreach (var g in allGOs)
                    {
                        if (g.name == targetName) { go = g; break; }
                    }
                }
                if (go != null)
                {
                    float dist = Vector3.Distance(cam.transform.position, go.transform.position);
                    float shadowDist = pipelineAsset != null ? pipelineAsset.shadowDistance : 0;
                    Debug.Log($"  Distance to {targetName}: {dist:F1}, ShadowDist={shadowDist}, " +
                              $"InRange={dist <= shadowDist}");
                }
            }
        }
        
        // 5. Check all Volumes
        var volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
        Debug.Log($"[VOLUMES] Found {volumes.Length} volumes in scene");
        foreach (var vol in volumes)
        {
            Debug.Log($"  Volume: {vol.name}, isGlobal={vol.isGlobal}, weight={vol.weight}, " +
                      $"priority={vol.priority}, profile={vol.profile?.name ?? "null"}");
        }
        
        // 6. Check QualitySettings
        Debug.Log($"[QUALITY] Level={QualitySettings.GetQualityLevel()}, " +
                  $"Name={QualitySettings.names[QualitySettings.GetQualityLevel()]}, " +
                  $"Shadows={QualitySettings.shadows}, " +
                  $"ShadowResolution={QualitySettings.shadowResolution}, " +
                  $"ShadowDistance={QualitySettings.shadowDistance}");
        
        Debug.Log("=== SHADOW DIAGNOSTICS END ===");
    }
}
