using System.Collections.Generic;
using UnityEngine;

namespace DragonCity
{
    [ExecuteInEditMode]
    public class CityBuilder : MonoBehaviour
    {
        [Header("City Dimensions")]
        public int gridWidth = 14;
        public int gridLength = 14;
        public float streetWidth = 18f;
        public float buildingFootprint = 12f;

        [Header("Building Heights")]
        public float minBuildingHeight = 15f;
        public float maxBuildingHeight = 90f;

        [Header("Colors & Visuals")]
        public Color asphaltColor = new Color(0.12f, 0.12f, 0.13f);
        public Color[] buildingColors = new Color[]
        {
            new Color(0.35f, 0.38f, 0.42f),
            new Color(0.25f, 0.28f, 0.35f),
            new Color(0.48f, 0.45f, 0.42f),
            new Color(0.20f, 0.22f, 0.25f),
            new Color(0.55f, 0.52f, 0.50f)
        };

        [Header("Generation Settings")]
        public bool generateOnStart = true;
        public int randomSeed = 1337;

        private GameObject cityRoot;

        private void Start()
        {
            if (generateOnStart && (!Application.isEditor || Application.isPlaying))
            {
                BuildCity();
            }
        }

        [ContextMenu("Regenerate City")]
        public void BuildCity()
        {
            // Clear existing city root
            if (cityRoot != null)
            {
                if (Application.isPlaying) Destroy(cityRoot);
                else DestroyImmediate(cityRoot);
            }

            Transform existing = transform.Find("City_Generated");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }

            cityRoot = new GameObject("City_Generated");
            cityRoot.transform.SetParent(transform, false);

            Random.InitState(randomSeed);

            // 1. Create Asphalt Ground
            CreateGround();

            // 2. Generate Buildings Grid
            CreateBuildings();

            Debug.Log($"[CityBuilder] Successfully generated city grid ({gridWidth}x{gridLength}) with colliders.");
        }

        private void CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Asphalt_Ground";
            ground.transform.SetParent(cityRoot.transform, false);

            float totalWidth = gridWidth * streetWidth;
            float totalLength = gridLength * streetWidth;

            // Plane default size is 10x10 units
            ground.transform.localScale = new Vector3(totalWidth * 0.15f, 1f, totalLength * 0.15f);
            ground.transform.position = new Vector3(0, 0, 0);

            Renderer rend = ground.GetComponent<Renderer>();
            if (rend != null)
            {
                Material groundMat = new Material(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Mobile/Diffuse"));
                groundMat.color = asphaltColor;
                rend.sharedMaterial = groundMat;
            }
        }

        private void CreateBuildings()
        {
            float halfW = (gridWidth - 1) * streetWidth * 0.5f;
            float halfL = (gridLength - 1) * streetWidth * 0.5f;

            Material baseMat = new Material(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Mobile/Diffuse"));

            int totalCreated = 0;

            for (int x = 0; x < gridWidth; x++)
            {
                for (int z = 0; z < gridLength; z++)
                {
                    // Leave a central runway / plaza for dragon launch
                    if (Mathf.Abs(x - gridWidth / 2) <= 1 && Mathf.Abs(z - gridLength / 2) <= 1)
                        continue;

                    totalCreated++;

                    float posX = (x * streetWidth) - halfW;
                    float posZ = (z * streetWidth) - halfL;

                    float height = Random.Range(minBuildingHeight, maxBuildingHeight);

                    // Add architectural variation
                    float footprintX = buildingFootprint * Random.Range(0.85f, 1.25f);
                    float footprintZ = buildingFootprint * Random.Range(0.85f, 1.25f);

                    GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    building.name = $"Building_{x}_{z}";
                    building.transform.SetParent(cityRoot.transform, false);
                    building.transform.position = new Vector3(posX, height * 0.5f, posZ);
                    building.transform.localScale = new Vector3(footprintX, height, footprintZ);
                    building.tag = "Untagged";

                    // Assign material with random facade tint
                    Renderer r = building.GetComponent<Renderer>();
                    if (r != null)
                    {
                        Material bMat = new Material(baseMat);
                        Color c = buildingColors[Random.Range(0, buildingColors.Length)];
                        bMat.color = c;
                        r.material = bMat;
                    }

                    // Add DestructibleBuilding component for physical collapse and destruction
                    building.AddComponent<DestructibleBuilding>();
                    building.AddComponent<BurnableBuilding>();
                }
            }

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.SetTotalBuildings(totalCreated);
                ScoreManager.Instance.ResetScore();
            }
        }
    }

    /// <summary>
    /// Legacy component kept for compatibility, forwards to DestructibleBuilding if present.
    /// </summary>
    public class BurnableBuilding : MonoBehaviour
    {
        private DestructibleBuilding destructible;
        private Renderer rend;
        private Color originalColor;
        private float scorchLevel = 0f;
        private static readonly Color ScorchColor = new Color(0.04f, 0.04f, 0.04f);

        private void Awake()
        {
            destructible = GetComponent<DestructibleBuilding>();
            rend = GetComponent<Renderer>();
            if (rend != null && rend.material != null)
            {
                originalColor = rend.material.color;
            }
        }

        public void ApplyScorch(float intensity)
        {
            if (destructible != null)
            {
                destructible.ApplyDamage(intensity * 15f, transform.position + Vector3.up * 5f, Vector3.up);
                return;
            }

            if (rend == null) return;
            scorchLevel = Mathf.Clamp01(scorchLevel + intensity);
            rend.material.color = Color.Lerp(originalColor, ScorchColor, scorchLevel);
        }
    }
}

