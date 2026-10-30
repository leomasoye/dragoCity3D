using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DragonCity
{
    public class DestructibleBuilding : MonoBehaviour
    {
        [Header("Durability & Health")]
        public float maxHealth = 60f;
        public float currentHealth;
        public bool isDestroyed = false;

        [Header("Visual Degradation")]
        public Color originalColor;
        public Color charredColor = new Color(0.06f, 0.06f, 0.06f);
        public Color emberColor = new Color(1.0f, 0.35f, 0.05f);

        [Header("Debris Configuration")]
        public int piecesX = 2;
        public int piecesZ = 2;
        public int piecesY = 4;
        public float explosionForceMin = 22f;
        public float explosionForceMax = 48f;
        public float debrisLifetime = 8f;

        private Renderer rend;
        private Material buildingMat;
        private Vector3 initialPosition;
        private Vector3 initialScale;
        private BoxCollider mainCollider;

        private void Awake()
        {
            rend = GetComponent<Renderer>();
            mainCollider = GetComponent<BoxCollider>();

            initialPosition = transform.position;
            initialScale = transform.localScale;

            // Health scales smoothly with building volume/height
            maxHealth = Mathf.Clamp(initialScale.y * 1.2f, 40f, 160f);
            currentHealth = maxHealth;

            if (rend != null)
            {
                // Create an instance of the material so each building burns uniquely
                buildingMat = new Material(rend.sharedMaterial ?? new Material(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Diffuse")));
                originalColor = buildingMat.color;
                rend.material = buildingMat;
            }
        }

        public void ApplyDamage(float damage, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (isDestroyed) return;

            currentHealth -= damage;
            float damageRatio = Mathf.Clamp01(1f - (currentHealth / maxHealth));

            // Visual charring and ember glow transition
            if (buildingMat != null)
            {
                Color targetCol = Color.Lerp(originalColor, charredColor, damageRatio);
                // When heavily damaged, add an incandescent ember tint
                if (damageRatio > 0.45f)
                {
                    float emberFactor = Mathf.PingPong(Time.time * 6f, 0.35f) * (damageRatio - 0.45f) * 2f;
                    targetCol = Color.Lerp(targetCol, emberColor, emberFactor);
                }
                buildingMat.color = targetCol;
            }

            // Visual impact spark/smoke puff at hit point
            SpawnHitPuff(hitPoint, hitNormal);

            // Minor damage score tick
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnDamageTick(damage);
            }

            // Structural shaking when critically damaged
            if (damageRatio > 0.65f)
            {
                float shakeAmount = (damageRatio - 0.65f) * 0.4f;
                transform.position = initialPosition + Random.insideUnitSphere * shakeAmount;
            }

            // Collapse trigger
            if (currentHealth <= 0f)
            {
                Collapse(hitPoint);
            }
        }

        private void Collapse(Vector3 impactPoint)
        {
            if (isDestroyed) return;
            isDestroyed = true;

            // Reset shake offset
            transform.position = initialPosition;

            // 1. Disable main monolith renderer and collider
            if (mainCollider != null) mainCollider.enabled = false;
            if (rend != null) rend.enabled = false;

            // 2. Camera Shake for player if within radius
            if (ThirdPersonCamera.Instance != null)
            {
                float dist = Vector3.Distance(transform.position, ThirdPersonCamera.Instance.transform.position);
                if (dist < 180f)
                {
                    float intensity = Mathf.Lerp(1.6f, 0.2f, dist / 180f);
                    ThirdPersonCamera.Instance.TriggerShake(intensity, 0.85f);
                }
            }

            // 3. Award Destruction Score & Trigger Combos
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnBuildingDestroyed(transform.position, initialScale.y, initialScale.x * initialScale.z);
            }

            // 4. Audio & Huge Smoke/Dust FX
            SpawnCollapseExplosionFX();
            PlayProceduralRumbleSound();

            // 5. Shatter Building into Physical Debris Pieces
            ShatterIntoPhysicalChunks(impactPoint);

            // 6. Leave a scorched rubble pile at the base
            CreateScorchedRubbleBase();
        }

        private void ShatterIntoPhysicalChunks(Vector3 impactPoint)
        {
            int countY = Mathf.Clamp(Mathf.RoundToInt(initialScale.y / 12f), 3, 6);
            Vector3 chunkSize = new Vector3(initialScale.x / piecesX, initialScale.y / countY, initialScale.z / piecesZ);

            Material charredDebrisMat = new Material(buildingMat != null ? buildingMat.shader : Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"));
            charredDebrisMat.color = new Color(0.12f, 0.12f, 0.13f);

            GameObject debrisContainer = new GameObject($"Debris_{name}");
            debrisContainer.transform.position = initialPosition;

            Vector3 baseCorner = initialPosition - (initialScale * 0.5f);

            for (int y = 0; y < countY; y++)
            {
                for (int x = 0; x < piecesX; x++)
                {
                    for (int z = 0; z < piecesZ; z++)
                    {
                        Vector3 chunkPos = baseCorner + new Vector3(
                            (x + 0.5f) * chunkSize.x,
                            (y + 0.5f) * chunkSize.y,
                            (z + 0.5f) * chunkSize.z
                        );

                        // Introduce slight randomized jitter in chunk dimensions
                        Vector3 actualSize = Vector3.Scale(chunkSize, new Vector3(
                            Random.Range(0.85f, 1.05f),
                            Random.Range(0.85f, 1.05f),
                            Random.Range(0.85f, 1.05f)
                        ));

                        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        piece.name = $"Chunk_{x}_{y}_{z}";
                        piece.transform.SetParent(debrisContainer.transform, true);
                        piece.transform.position = chunkPos;
                        piece.transform.localScale = actualSize;

                        Renderer pr = piece.GetComponent<Renderer>();
                        if (pr != null) pr.sharedMaterial = charredDebrisMat;

                        Rigidbody rb = piece.AddComponent<Rigidbody>();
                        rb.mass = actualSize.x * actualSize.y * actualSize.z * 1.5f;
#if UNITY_6000_0_OR_NEWER
                        rb.linearDamping = 0.4f;
                        rb.angularDamping = 0.8f;
#else
                        rb.drag = 0.4f;
                        rb.angularDrag = 0.8f;
#endif
                        rb.interpolation = RigidbodyInterpolation.Interpolate;

                        // Radial impulse outwards + upwards
                        Vector3 dirOut = (chunkPos - initialPosition).normalized;
                        dirOut.y = Mathf.Abs(dirOut.y) + 0.6f;
                        float force = Random.Range(explosionForceMin, explosionForceMax);
                        rb.AddForce(dirOut.normalized * force, ForceMode.Impulse);
                        rb.AddTorque(Random.insideUnitSphere * Random.Range(10f, 35f), ForceMode.Impulse);

                        // Attach shrink and cleanup behavior
                        piece.AddComponent<DebrisPiece>().Initialize(debrisLifetime + Random.Range(-1.5f, 2.5f));
                    }
                }
            }

            // Destroy container after maximum lifetime
            Destroy(debrisContainer, debrisLifetime + 4f);
        }

        private void CreateScorchedRubbleBase()
        {
            GameObject rubble = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rubble.name = $"{name}_Rubble";
            rubble.transform.position = new Vector3(initialPosition.x, 1.0f, initialPosition.z);
            rubble.transform.localScale = new Vector3(initialScale.x * 1.1f, 1.8f, initialScale.z * 1.1f);

            Renderer r = rubble.GetComponent<Renderer>();
            if (r != null)
            {
                Material rMat = new Material(buildingMat != null ? buildingMat.shader : Shader.Find("Standard") ?? Shader.Find("Diffuse"));
                rMat.color = new Color(0.05f, 0.05f, 0.05f);
                r.sharedMaterial = rMat;
            }
        }

        private void SpawnCollapseExplosionFX()
        {
            GameObject fxObj = new GameObject("Collapse_FX");
            fxObj.transform.position = initialPosition + Vector3.up * (initialScale.y * 0.35f);

            ParticleSystem ps = fxObj.AddComponent<ParticleSystem>();
            var psRend = fxObj.GetComponent<ParticleSystemRenderer>();

            var main = ps.main;
            main.playOnAwake = true;
            main.loop = false;
            main.duration = 1.0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.0f, 5.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(10f, 32f);
            main.startSize = new ParticleSystem.MinMaxCurve(5f, 16f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Fiery dust to billowing grey/black smoke
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0.0f),
                    new GradientColorKey(new Color(0.35f, 0.35f, 0.35f), 0.25f),
                    new GradientColorKey(new Color(0.1f, 0.1f, 0.1f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.85f, 0.0f),
                    new GradientAlphaKey(0.7f, 0.4f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            main.startColor = new ParticleSystem.MinMaxGradient(grad);

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0.0f, 80, 140)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(initialScale.x, 4f, initialScale.z);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sc = new AnimationCurve();
            sc.AddKey(0f, 0.3f);
            sc.AddKey(1f, 2.8f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sc);

            Shader pShader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Mobile/Particles/Additive");
            if (pShader != null)
            {
                psRend.material = new Material(pShader);
            }

            Destroy(fxObj, 6.5f);
        }

        private void SpawnHitPuff(Vector3 hitPos, Vector3 hitNormal)
        {
            // Lightweight visual puff for real-time bullet/flame contact
            GameObject puff = new GameObject("Hit_Sparks");
            puff.transform.position = hitPos;

            ParticleSystem ps = puff.AddComponent<ParticleSystem>();
            var psRend = puff.GetComponent<ParticleSystemRenderer>();

            var main = ps.main;
            main.loop = false;
            main.duration = 0.2f;
            main.startLifetime = 0.5f;
            main.startSpeed = Random.Range(3f, 8f);
            main.startSize = Random.Range(0.8f, 2.2f);
            main.startColor = new Color(1f, 0.4f, 0.05f, 0.8f);

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 6) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f;

            Shader s = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Mobile/Particles/Additive");
            if (s != null) psRend.material = new Material(s);

            Destroy(puff, 0.6f);
        }

        private void PlayProceduralRumbleSound()
        {
            GameObject audioObj = new GameObject("Collapse_Audio");
            audioObj.transform.position = initialPosition;

            AudioSource source = audioObj.AddComponent<AudioSource>();
            source.spatialBlend = 1.0f; // Full 3D audio
            source.minDistance = 20f;
            source.maxDistance = 250f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.volume = 0.85f;

            // Generate procedural low-frequency rumble waveform
            int sampleRate = 22050;
            float duration = 1.8f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 2.2f);
                float noise = (Random.value * 2f - 1f) * 0.7f;
                float lowSine = Mathf.Sin(2f * Mathf.PI * 45f * t) * 0.4f;
                float subBass = Mathf.Sin(2f * Mathf.PI * 28f * t) * 0.3f;
                samples[i] = (noise + lowSine + subBass) * envelope;
            }

            AudioClip clip = AudioClip.Create("BuildingCollapseRumble", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);

            source.clip = clip;
            source.Play();

            Destroy(audioObj, duration + 0.5f);
        }
    }

    /// <summary>
    /// Smoothly shrinks and destroys debris pieces after settling on ground to preserve performance.
    /// </summary>
    public class DebrisPiece : MonoBehaviour
    {
        private float lifeTimer;
        private Vector3 baseScale;
        private bool isShrinking = false;

        public void Initialize(float lifetime)
        {
            lifeTimer = lifetime;
            baseScale = transform.localScale;
        }

        private void Update()
        {
            lifeTimer -= Time.deltaTime;

            if (lifeTimer <= 1.5f && !isShrinking)
            {
                isShrinking = true;
            }

            if (isShrinking)
            {
                float factor = Mathf.Clamp01(lifeTimer / 1.5f);
                transform.localScale = Vector3.Lerp(Vector3.zero, baseScale, factor);
                if (lifeTimer <= 0f)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}

