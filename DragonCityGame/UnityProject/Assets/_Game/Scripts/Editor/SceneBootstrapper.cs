using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using DragonCity;

public class SceneBootstrapper
{
    private const string ScenePath = "Assets/_Game/Scenes/MainCity.unity";
    private const string DragonModelPath = "Assets/_Game/Models/dragon.fbx";

    [MenuItem("DragonCity/Regenerate Complete Scene (With Dragon FBX)")]
    public static void BuildScene()
    {
        Debug.Log("[SceneBootstrapper] Starting headless scene bootstrap...");

        try
        {
            // 1. Ensure directory exists
            string sceneDir = Path.GetDirectoryName(ScenePath);
            if (!Directory.Exists(sceneDir))
            {
                Directory.CreateDirectory(sceneDir);
            }

            // 2. Create a brand new Scene
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 3. Directional Light & Environment Setup
            GameObject lightObj = new GameObject("Directional Light");
            Light dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(1f, 0.95f, 0.85f);
            dirLight.intensity = 1.3f;
            dirLight.shadows = LightShadows.Soft;
            lightObj.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            // 4. City Builder Manager
            GameObject cityManager = new GameObject("City_Manager");
            CityBuilder builder = cityManager.AddComponent<CityBuilder>();
            builder.gridWidth = 16;
            builder.gridLength = 16;
            builder.streetWidth = 20f;
            builder.buildingFootprint = 14f;
            builder.minBuildingHeight = 15f;
            builder.maxBuildingHeight = 110f;
            builder.generateOnStart = true;
            
            // Generate geometry immediately for editor & play
            builder.BuildCity();

            // 5. Instantiate Dragon Player
            GameObject dragonRoot = new GameObject("Dragon_Player");
            dragonRoot.transform.position = new Vector3(0f, 65f, 0f); // Spawn high above the city center
            dragonRoot.transform.rotation = Quaternion.identity;

            // Load & instantiate dragon FBX model as child
            GameObject dragonFbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DragonModelPath);
            if (dragonFbxPrefab != null)
            {
                GameObject visualInstance = (GameObject)PrefabUtility.InstantiatePrefab(dragonFbxPrefab);
                visualInstance.name = "Dragon_Visual";
                visualInstance.transform.SetParent(dragonRoot.transform, false);
                visualInstance.transform.localPosition = Vector3.zero;
                visualInstance.transform.localRotation = Quaternion.identity;
                Debug.Log("[SceneBootstrapper] Successfully loaded and attached dragon.fbx visual model.");
            }
            else
            {
                Debug.LogWarning($"[SceneBootstrapper] Could not load FBX at {DragonModelPath}. Creating procedural placeholder body.");
                GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                placeholder.name = "Dragon_Visual_Fallback";
                placeholder.transform.SetParent(dragonRoot.transform, false);
                placeholder.transform.localScale = new Vector3(2f, 1f, 4f);
            }

            // Attach Rigidbody and Flight Controller
            Rigidbody rb = dragonRoot.AddComponent<Rigidbody>();
            rb.useGravity = false;
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = 1.5f;
            rb.angularDamping = 2.5f;
#else
            rb.drag = 1.5f;
            rb.angularDrag = 2.5f;
#endif
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Box Collider bounding the dragon body
            BoxCollider col = dragonRoot.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.7f, 0.5f);
            col.size = new Vector3(4.5f, 2.0f, 6.0f);

            // Flight controller
            DragonFlightController flightCtrl = dragonRoot.AddComponent<DragonFlightController>();
            flightCtrl.baseCruiseSpeed = 35f;

            // Breath Weapon
            DragonBreath breath = dragonRoot.AddComponent<DragonBreath>();

            // 6. Camera Rig
            GameObject camObj = new GameObject("Main Camera");
            Camera cam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 2000f;
            cam.clearFlags = CameraClearFlags.Skybox;
            camObj.AddComponent<AudioListener>();

            ThirdPersonCamera tpCam = camObj.AddComponent<ThirdPersonCamera>();
            tpCam.target = dragonRoot.transform;
            tpCam.followOffset = new Vector3(0f, 5f, -14f);

            // Snap camera immediately behind dragon
            camObj.transform.position = dragonRoot.transform.position + new Vector3(0f, 5f, -14f);
            camObj.transform.LookAt(dragonRoot.transform.position + Vector3.forward * 20f);

            // 7. Save Scene to Assets
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[SceneBootstrapper] Scene saved successfully to {ScenePath}: {saved}");

            // 8. Register Scene in EditorBuildSettings
            EditorBuildSettingsScene[] originalScenes = EditorBuildSettings.scenes;
            bool existsInBuild = false;
            foreach (var s in originalScenes)
            {
                if (s.path == ScenePath)
                {
                    existsInBuild = true;
                    s.enabled = true;
                    break;
                }
            }

            if (!existsInBuild)
            {
                EditorBuildSettingsScene[] newScenes = new EditorBuildSettingsScene[originalScenes.Length + 1];
                for (int i = 0; i < originalScenes.Length; i++)
                {
                    newScenes[i] = originalScenes[i];
                }
                newScenes[originalScenes.Length] = new EditorBuildSettingsScene(ScenePath, true);
                EditorBuildSettings.scenes = newScenes;
                Debug.Log($"[SceneBootstrapper] Added {ScenePath} to EditorBuildSettings.scenes.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SceneBootstrapper] Scene build completed successfully!");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SceneBootstrapper] Fatal error during scene generation: {ex}");
            EditorApplication.Exit(1);
        }
    }
}

