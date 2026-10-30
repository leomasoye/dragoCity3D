using System.Collections.Generic;
using UnityEngine;

namespace DragonCity
{
    public class DragonBreath : MonoBehaviour
    {
        [Header("Breath Controls")]
        public KeyCode fireKey = KeyCode.Space;
        public bool enableMouseFire = true;
        public float minBurstDuration = 0.42f; // Guarantees a powerful fire blast even on a single quick tap
        public Transform breathSpawnPoint;

        [Header("Long-Range Flame Parameters")]
        public float flameRange = 175f;        // Extended long range: 175 meters
        public float flameSpeed = 105f;        // High velocity projection
        public float damagePerParticle = 11.5f; // Fast, satisfying demolition
        public float burnDamageIntensity = 0.35f;

        [Header("Rank Evolution Stats")]
        public int currentRank = 0;
        private static readonly float[] RANK_FLAME_RANGES = new float[] { 175f, 220f, 275f, 340f, 415f, 500f };
        private static readonly float[] RANK_FLAME_SPEEDS = new float[] { 105f, 125f, 150f, 180f, 215f, 260f };
        private static readonly float[] RANK_DAMAGE_PER_PARTICLE = new float[] { 11.5f, 17f, 25f, 36f, 52f, 75f };
        private static readonly float[] RANK_PARTICLE_MIN_SIZE = new float[] { 1.5f, 2.0f, 2.7f, 3.6f, 4.8f, 6.5f };
        private static readonly float[] RANK_PARTICLE_MAX_SIZE = new float[] { 4.2f, 5.6f, 7.5f, 9.8f, 12.8f, 17.5f };
        private static readonly float[] RANK_EMISSION_RATES = new float[] { 280f, 350f, 440f, 550f, 680f, 850f };
        private static readonly float[] RANK_LIGHT_RANGES = new float[] { 75f, 95f, 125f, 165f, 215f, 280f };

        [Header("Visual Effects")]
        public ParticleSystem flameParticles;
        public ParticleSystem sparksParticles;
        public Light breathLight;

        [Header("HUD Reticle & Feedback")]
        public bool showHUD = true;

        private bool isBreathing = false;
        private float burstTimer = 0f;
        private AudioSource breathAudio;
        private Texture2D whitePixelTex;

        public bool IsBreathing => isBreathing;

        private void Awake()
        {
            if (flameRange < 120f) flameRange = 175f;
            if (flameSpeed < 70f) flameSpeed = 105f;

            if (flameParticles == null)
            {
                SetupProceduralParticleSystem();
            }

            if (breathLight == null)
            {
                GameObject lightObj = new GameObject("BreathLight");
                lightObj.transform.SetParent(transform, false);
                lightObj.transform.localPosition = new Vector3(0f, 1.2f, 4.0f);
                breathLight = lightObj.AddComponent<Light>();
                breathLight.type = LightType.Point;
                breathLight.color = new Color(1f, 0.45f, 0.08f);
                breathLight.range = 75f;
                breathLight.intensity = 0f;
            }

            SetupProceduralFlameAudio();
        }

        private void Update()
        {
            // 1. Detect Fire Trigger: Left Mouse Click, Spacebar, or 'E'
            bool clickDown = (enableMouseFire && Input.GetMouseButtonDown(0)) || Input.GetKeyDown(fireKey) || Input.GetKeyDown(KeyCode.E);
            bool clickHeld = (enableMouseFire && Input.GetMouseButton(0)) || Input.GetKey(fireKey) || Input.GetKey(KeyCode.E);

            if (clickDown)
            {
                burstTimer = minBurstDuration;

                // Immediate particle emission scaled with dragon rank
                int burstFlame = Mathf.RoundToInt(Mathf.Lerp(25f, 65f, currentRank / 5f));
                int burstSparks = Mathf.RoundToInt(Mathf.Lerp(12f, 35f, currentRank / 5f));

                if (flameParticles != null) flameParticles.Emit(burstFlame);
                if (sparksParticles != null) sparksParticles.Emit(burstSparks);

                float shakePower = Mathf.Lerp(0.35f, 0.85f, currentRank / 5f);
                ThirdPersonCamera.Instance?.TriggerShake(shakePower, 0.25f);
            }

            if (clickHeld)
            {
                burstTimer = Mathf.Max(burstTimer, 0.08f);
            }
            else if (burstTimer > 0f)
            {
                burstTimer -= Time.deltaTime;
            }

            isBreathing = burstTimer > 0f;

            // 2. Converge Flame Stream Towards Crosshair Target
            Ray aimRay = Camera.main != null ? Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)) : new Ray(transform.position, transform.forward);
            Vector3 targetAimPos = aimRay.GetPoint(flameRange);
            if (Physics.Raycast(aimRay, out RaycastHit hit, flameRange, ~LayerMask.GetMask("Player", "Ignore Raycast")))
            {
                targetAimPos = hit.point;
            }

            if (flameParticles != null)
            {
                flameParticles.transform.LookAt(targetAimPos);
                var emission = flameParticles.emission;
                emission.enabled = isBreathing;
                if (isBreathing && !flameParticles.isPlaying) flameParticles.Play();
            }

            if (sparksParticles != null)
            {
                sparksParticles.transform.LookAt(targetAimPos);
                var emissionSparks = sparksParticles.emission;
                emissionSparks.enabled = isBreathing;
                if (isBreathing && !sparksParticles.isPlaying) sparksParticles.Play();
            }

            // 3. Dynamic Snout Lighting Flare
            if (breathLight != null)
            {
                float targetIntensity = isBreathing ? Random.Range(7.5f, 11.5f) : 0f;
                breathLight.intensity = Mathf.Lerp(breathLight.intensity, targetIntensity, Time.deltaTime * 24f);
            }

            // 4. Procedural Fire Audio Volume Modulation
            if (breathAudio != null)
            {
                float targetVolume = isBreathing ? 0.85f : 0f;
                breathAudio.volume = Mathf.Lerp(breathAudio.volume, targetVolume, Time.deltaTime * 16f);
                if (isBreathing && !breathAudio.isPlaying)
                {
                    breathAudio.Play();
                }
            }
        }

        private void SetupProceduralFlameAudio()
        {
            breathAudio = gameObject.AddComponent<AudioSource>();
            breathAudio.loop = true;
            breathAudio.playOnAwake = false;
            breathAudio.spatialBlend = 0.35f;
            breathAudio.volume = 0f;
            breathAudio.pitch = 0.95f;

            int sampleRate = 22050;
            int lengthSamples = sampleRate * 2;
            float[] samples = new float[lengthSamples];
            float filterState = 0f;

            for (int i = 0; i < lengthSamples; i++)
            {
                float whiteNoise = Random.Range(-1.0f, 1.0f);
                filterState = Mathf.Lerp(filterState, whiteNoise, 0.12f);
                float crackle = (Random.value > 0.985f) ? Random.Range(0.4f, 0.9f) : 0f;
                samples[i] = (filterState * 0.75f + crackle) * 0.45f;
            }

            AudioClip flameClip = AudioClip.Create("DragonFlameRoarProcedural", lengthSamples, 1, sampleRate, false);
            flameClip.SetData(samples, 0);
            breathAudio.clip = flameClip;
        }

        private void OnGUI()
        {
            if (!showHUD) return;

            if (whitePixelTex == null)
            {
                whitePixelTex = new Texture2D(1, 1);
                whitePixelTex.SetPixel(0, 0, Color.white);
                whitePixelTex.Apply();
            }

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            Color hudColor = isBreathing ? new Color(1f, 0.45f, 0.1f, 0.95f) : new Color(1f, 1f, 1f, 0.65f);
            Color oldGuiColor = GUI.color;
            GUI.color = hudColor;

            // Center targeting reticle: Center Dot
            float dotSize = isBreathing ? 6f : 4f;
            GUI.DrawTexture(new Rect(cx - dotSize * 0.5f, cy - dotSize * 0.5f, dotSize, dotSize), whitePixelTex);

            // Crosshair Brackets
            float bracketDist = isBreathing ? 24f : 16f;
            float bracketLen = 10f;
            float bracketThick = 2f;

            // Left Bracket [
            GUI.DrawTexture(new Rect(cx - bracketDist - bracketThick, cy - bracketLen * 0.5f, bracketThick, bracketLen), whitePixelTex);
            // Right Bracket ]
            GUI.DrawTexture(new Rect(cx + bracketDist, cy - bracketLen * 0.5f, bracketThick, bracketLen), whitePixelTex);

            // Top / Bottom subtle pips
            GUI.DrawTexture(new Rect(cx - bracketLen * 0.5f, cy - bracketDist - bracketThick, bracketLen, bracketThick), whitePixelTex);
            GUI.DrawTexture(new Rect(cx - bracketLen * 0.5f, cy + bracketDist, bracketLen, bracketThick), whitePixelTex);

            // Controls Hint Banner at Bottom
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f, 0.9f);

            string hintText = isBreathing ? "🔥 SOUFFLE DRACONIQUE ACTIF !" : "Souris : Diriger & Viser  |  Clic Gauche : Cracher du Feu  |  Z / W : Voler  |  Échap : Curseur";
            GUI.Label(new Rect(0, Screen.height - 38, Screen.width, 28), hintText, labelStyle);

            GUI.color = oldGuiColor;
        }

        private void SetupProceduralParticleSystem()
        {
            GameObject psObj = new GameObject("DragonFlameEmitter");
            psObj.transform.SetParent(breathSpawnPoint != null ? breathSpawnPoint : transform, false);
            psObj.transform.localPosition = new Vector3(0f, 1.2f, 3.8f); // Snout position
            psObj.transform.localRotation = Quaternion.identity;

            flameParticles = psObj.AddComponent<ParticleSystem>();
            var psRend = psObj.GetComponent<ParticleSystemRenderer>();

            // 1. Main Torrent Module
            var main = flameParticles.main;
            main.playOnAwake = true;
            main.loop = true;
            main.startLifetime = flameRange / flameSpeed; // ~1.65 seconds to reach 175m
            main.startSpeed = flameSpeed;
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 4.2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Gradient: Blinding Yellow/White -> Fiery Solar Orange -> Deep Blood Crimson -> Volcanic Ash
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 0.98f, 0.65f), 0.0f),
                    new GradientColorKey(new Color(1f, 0.50f, 0.04f), 0.28f),
                    new GradientColorKey(new Color(0.92f, 0.18f, 0.02f), 0.65f),
                    new GradientColorKey(new Color(0.12f, 0.12f, 0.14f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.95f, 0.0f),
                    new GradientAlphaKey(0.95f, 0.2f),
                    new GradientAlphaKey(0.85f, 0.65f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            main.startColor = new ParticleSystem.MinMaxGradient(gradient);

            // Emission Module (Intense, continuous flow)
            var emission = flameParticles.emission;
            emission.rateOverTime = 280f;
            emission.enabled = false;

            // Shape Module (Focused Cone)
            var shape = flameParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8.5f;
            shape.radius = 0.35f;

            // Massive Size expansion over lifetime (billowing fire stream)
            var sizeOverLifetime = flameParticles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.5f);
            sizeCurve.AddKey(0.4f, 1.8f);
            sizeCurve.AddKey(1f, 3.8f); // Expands up to ~15m wide at max distance
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Collision Module
            var collision = flameParticles.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.sendCollisionMessages = true;
            collision.bounce = 0.05f;
            collision.lifetimeLoss = 0.2f; // Retains velocity on contact to wrap around geometry
            collision.minKillSpeed = 0f;

            // Particle Material
            Shader partShader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Mobile/Particles/Additive");
            if (partShader != null)
            {
                Material pMat = new Material(partShader);
                psRend.material = pMat;
            }

            // 2. High-speed incandescent embers & sparks
            GameObject sparksObj = new GameObject("FlameSparks");
            sparksObj.transform.SetParent(psObj.transform, false);
            sparksParticles = sparksObj.AddComponent<ParticleSystem>();
            var sparksRend = sparksObj.GetComponent<ParticleSystemRenderer>();

            var sMain = sparksParticles.main;
            sMain.loop = true;
            sMain.playOnAwake = true;
            sMain.startLifetime = (flameRange / flameSpeed) * 1.15f;
            sMain.startSpeed = flameSpeed * 1.1f;
            sMain.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            sMain.startColor = new Color(1f, 0.85f, 0.3f, 1f);
            sMain.simulationSpace = ParticleSystemSimulationSpace.World;

            var sEmission = sparksParticles.emission;
            sEmission.rateOverTime = 120f;
            sEmission.enabled = false;

            var sShape = sparksParticles.shape;
            sShape.shapeType = ParticleSystemShapeType.Cone;
            sShape.angle = 7.0f;
            sShape.radius = 0.2f;

            if (partShader != null)
            {
                sparksRend.material = new Material(partShader);
            }

            // Forward collisions to this script
            var colForwarder = psObj.AddComponent<FlameCollisionRelay>();
            colForwarder.parentBreath = this;
            colForwarder.ps = flameParticles;
        }

        public void HandleParticleHit(GameObject other, Vector3 hitPoint, Vector3 hitNormal, int particleCount)
        {
            if (other == null) return;

            // 1. Check for Destructible Building
            DestructibleBuilding destructible = other.GetComponent<DestructibleBuilding>() ?? other.GetComponentInParent<DestructibleBuilding>();
            if (destructible != null)
            {
                float totalDamage = damagePerParticle * Mathf.Max(1, particleCount);
                destructible.ApplyDamage(totalDamage, hitPoint, hitNormal);
                return;
            }

            // 2. Check for Debris piece (blast debris through the streets)
            Rigidbody debrisRb = other.GetComponent<Rigidbody>();
            if (debrisRb != null && other.GetComponent<DebrisPiece>() != null)
            {
                debrisRb.AddForce(-hitNormal * 18f * particleCount, ForceMode.Impulse);
                return;
            }

            // 3. Fallback for Burnable Building
            BurnableBuilding burnable = other.GetComponent<BurnableBuilding>();
            if (burnable != null)
            {
                burnable.ApplyScorch(burnDamageIntensity * particleCount);
                return;
            }

            // 4. Fallback for any standard mesh renderer in the city
            Renderer rend = other.GetComponent<Renderer>();
            if (rend != null && rend.material != null)
            {
                Color current = rend.material.color;
                Color charred = Color.Lerp(current, new Color(0.04f, 0.04f, 0.04f), burnDamageIntensity);
                rend.material.color = charred;
            }
        }

        public void ApplyRankEvolution(int rankIndex)
        {
            currentRank = Mathf.Clamp(rankIndex, 0, RANK_FLAME_RANGES.Length - 1);

            flameRange = RANK_FLAME_RANGES[currentRank];
            flameSpeed = RANK_FLAME_SPEEDS[currentRank];
            damagePerParticle = RANK_DAMAGE_PER_PARTICLE[currentRank];
            burnDamageIntensity = Mathf.Lerp(0.35f, 0.75f, currentRank / 5f);

            if (flameParticles != null)
            {
                var main = flameParticles.main;
                main.startLifetime = flameRange / flameSpeed;
                main.startSpeed = flameSpeed;
                main.startSize = new ParticleSystem.MinMaxCurve(RANK_PARTICLE_MIN_SIZE[currentRank], RANK_PARTICLE_MAX_SIZE[currentRank]);
                main.startColor = new ParticleSystem.MinMaxGradient(GetRankGradient(currentRank));

                var emission = flameParticles.emission;
                emission.rateOverTime = RANK_EMISSION_RATES[currentRank];

                var shape = flameParticles.shape;
                shape.angle = Mathf.Lerp(8.5f, 13.5f, currentRank / 5f);
                shape.radius = Mathf.Lerp(0.35f, 1.15f, currentRank / 5f);
            }

            if (sparksParticles != null)
            {
                var sMain = sparksParticles.main;
                sMain.startLifetime = (flameRange / flameSpeed) * 1.15f;
                sMain.startSpeed = flameSpeed * 1.1f;
                sMain.startSize = new ParticleSystem.MinMaxCurve(RANK_PARTICLE_MIN_SIZE[currentRank] * 0.25f, RANK_PARTICLE_MIN_SIZE[currentRank] * 0.5f);
                sMain.startColor = GetRankSparkColor(currentRank);

                var sEmission = sparksParticles.emission;
                sEmission.rateOverTime = RANK_EMISSION_RATES[currentRank] * 0.45f;
            }

            if (breathLight != null)
            {
                breathLight.range = RANK_LIGHT_RANGES[currentRank];
                breathLight.color = GetRankLightColor(currentRank);
            }

            if (breathAudio != null)
            {
                // Deeper, heavier bass roar as the dragon reaches titan proportions
                breathAudio.pitch = Mathf.Lerp(0.95f, 0.72f, currentRank / 5f);
            }

            Debug.Log($"[DragonBreath] Fire evolved to Rank {currentRank}! Range: {flameRange}m, Damage: {damagePerParticle}/part, Rate: {RANK_EMISSION_RATES[currentRank]}");
        }

        private Gradient GetRankGradient(int rank)
        {
            Gradient grad = new Gradient();

            switch (rank)
            {
                case 1: // Pilleur Ardent (Amber Fire)
                    grad.SetKeys(
                        new GradientColorKey[] {
                            new GradientColorKey(new Color(1f, 1f, 0.7f), 0.0f),
                            new GradientColorKey(new Color(1f, 0.55f, 0.08f), 0.28f),
                            new GradientColorKey(new Color(0.95f, 0.15f, 0.02f), 0.65f),
                            new GradientColorKey(new Color(0.15f, 0.12f, 0.12f), 1.0f)
                        },
                        new GradientAlphaKey[] {
                            new GradientAlphaKey(0.95f, 0.0f),
                            new GradientAlphaKey(0.95f, 0.25f),
                            new GradientAlphaKey(0.85f, 0.65f),
                            new GradientAlphaKey(0.0f, 1.0f)
                        }
                    );
                    break;

                case 2: // Fléau Urbain (Solar White Gold)
                    grad.SetKeys(
                        new GradientColorKey[] {
                            new GradientColorKey(new Color(1f, 1f, 0.95f), 0.0f),
                            new GradientColorKey(new Color(1f, 0.78f, 0.12f), 0.28f),
                            new GradientColorKey(new Color(1f, 0.32f, 0.05f), 0.65f),
                            new GradientColorKey(new Color(0.2f, 0.18f, 0.15f), 1.0f)
                        },
                        new GradientAlphaKey[] {
                            new GradientAlphaKey(1.0f, 0.0f),
                            new GradientAlphaKey(0.95f, 0.3f),
                            new GradientAlphaKey(0.85f, 0.7f),
                            new GradientAlphaKey(0.0f, 1.0f)
                        }
                    );
                    break;

                case 3: // Dévoreur de Cité (Volcanic Magma)
                    grad.SetKeys(
                        new GradientColorKey[] {
                            new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0.0f),
                            new GradientColorKey(new Color(1f, 0.22f, 0.05f), 0.32f),
                            new GradientColorKey(new Color(0.7f, 0.04f, 0.02f), 0.7f),
                            new GradientColorKey(new Color(0.08f, 0.08f, 0.08f), 1.0f)
                        },
                        new GradientAlphaKey[] {
                            new GradientAlphaKey(1.0f, 0.0f),
                            new GradientAlphaKey(0.95f, 0.35f),
                            new GradientAlphaKey(0.9f, 0.75f),
                            new GradientAlphaKey(0.0f, 1.0f)
                        }
                    );
                    break;

                case 4: // Seigneur des Cendres (Blood & Brimstone)
                    grad.SetKeys(
                        new GradientColorKey[] {
                            new GradientColorKey(new Color(0.85f, 0.95f, 1.0f), 0.0f), // Superheated blue core
                            new GradientColorKey(new Color(0.95f, 0.15f, 0.08f), 0.28f),
                            new GradientColorKey(new Color(0.5f, 0.02f, 0.04f), 0.65f),
                            new GradientColorKey(new Color(0.04f, 0.04f, 0.05f), 1.0f)
                        },
                        new GradientAlphaKey[] {
                            new GradientAlphaKey(1.0f, 0.0f),
                            new GradientAlphaKey(0.95f, 0.35f),
                            new GradientAlphaKey(0.9f, 0.75f),
                            new GradientAlphaKey(0.0f, 1.0f)
                        }
                    );
                    break;

                case 5: // APOCALYPSE DRACONIQUE (Cosmic Plasma)
                    grad.SetKeys(
                        new GradientColorKey[] {
                            new GradientColorKey(new Color(1f, 1f, 1f), 0.0f), // Blinding solar white
                            new GradientColorKey(new Color(0.98f, 0.15f, 0.85f), 0.25f), // Electric Neon Magenta
                            new GradientColorKey(new Color(0.45f, 0.08f, 0.95f), 0.65f), // Cosmic Deep Violet
                            new GradientColorKey(new Color(0.05f, 0.02f, 0.12f), 1.0f)
                        },
                        new GradientAlphaKey[] {
                            new GradientAlphaKey(1.0f, 0.0f),
                            new GradientAlphaKey(1.0f, 0.3f),
                            new GradientAlphaKey(0.9f, 0.7f),
                            new GradientAlphaKey(0.0f, 1.0f)
                        }
                    );
                    break;

                default: // Rank 0: Classic Fire
                    grad.SetKeys(
                        new GradientColorKey[] {
                            new GradientColorKey(new Color(1f, 0.98f, 0.65f), 0.0f),
                            new GradientColorKey(new Color(1f, 0.50f, 0.04f), 0.28f),
                            new GradientColorKey(new Color(0.92f, 0.18f, 0.02f), 0.65f),
                            new GradientColorKey(new Color(0.12f, 0.12f, 0.14f), 1.0f)
                        },
                        new GradientAlphaKey[] {
                            new GradientAlphaKey(0.95f, 0.0f),
                            new GradientAlphaKey(0.95f, 0.2f),
                            new GradientAlphaKey(0.85f, 0.65f),
                            new GradientAlphaKey(0.0f, 1.0f)
                        }
                    );
                    break;
            }

            return grad;
        }

        private Color GetRankSparkColor(int rank)
        {
            switch (rank)
            {
                case 1: return new Color(1f, 0.7f, 0.2f, 1f);
                case 2: return new Color(1f, 0.95f, 0.4f, 1f);
                case 3: return new Color(1f, 0.35f, 0.1f, 1f);
                case 4: return new Color(0.95f, 0.15f, 0.1f, 1f);
                case 5: return new Color(0.95f, 0.2f, 0.95f, 1f); // Neon Magenta Plasma Sparks
                default: return new Color(1f, 0.85f, 0.3f, 1f);
            }
        }

        private Color GetRankLightColor(int rank)
        {
            switch (rank)
            {
                case 1: return new Color(1f, 0.5f, 0.1f);
                case 2: return new Color(1f, 0.75f, 0.25f);
                case 3: return new Color(1f, 0.3f, 0.05f);
                case 4: return new Color(0.95f, 0.15f, 0.1f);
                case 5: return new Color(0.95f, 0.25f, 0.95f);
                default: return new Color(1f, 0.45f, 0.08f);
            }
        }
    }

    /// <summary>
    /// Relays particle collision events back to the parent DragonBreath component with exact impact data.
    /// </summary>
    public class FlameCollisionRelay : MonoBehaviour
    {
        public DragonBreath parentBreath;
        public ParticleSystem ps;
        private List<ParticleCollisionEvent> colEvents = new List<ParticleCollisionEvent>();

        private void OnParticleCollision(GameObject other)
        {
            if (parentBreath == null || ps == null) return;

            int count = ps.GetCollisionEvents(other, colEvents);
            if (count > 0)
            {
                Vector3 hitPos = colEvents[0].intersection;
                Vector3 hitNorm = colEvents[0].normal;
                parentBreath.HandleParticleHit(other, hitPos, hitNorm, count);
            }
            else
            {
                parentBreath.HandleParticleHit(other, other.transform.position, Vector3.up, 1);
            }
        }
    }
}
