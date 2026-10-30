using System.Collections.Generic;
using UnityEngine;

namespace DragonCity
{
    public class DragonBreath : MonoBehaviour
    {
        [Header("Breath Controls")]
        public KeyCode fireKey = KeyCode.Space;
        public Transform breathSpawnPoint;

        [Header("Long-Range Flame Parameters")]
        public float flameRange = 175f;        // Extended long range: 175 meters
        public float flameSpeed = 105f;        // High velocity projection
        public float damagePerParticle = 11.5f; // Fast, satisfying demolition
        public float burnDamageIntensity = 0.35f;

        [Header("Visual Effects")]
        public ParticleSystem flameParticles;
        public ParticleSystem sparksParticles;
        public Light breathLight;

        private bool isBreathing = false;

        private void Awake()
        {
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
        }

        private void Update()
        {
            // Fire triggered by Space, Left Click, or 'E'
            isBreathing = Input.GetKey(fireKey) || Input.GetMouseButton(0) || Input.GetKey(KeyCode.E);

            if (flameParticles != null)
            {
                var emission = flameParticles.emission;
                emission.enabled = isBreathing;
            }

            if (sparksParticles != null)
            {
                var emissionSparks = sparksParticles.emission;
                emissionSparks.enabled = isBreathing;
            }

            if (breathLight != null)
            {
                float targetIntensity = isBreathing ? Random.Range(6.5f, 9.5f) : 0f;
                breathLight.intensity = Mathf.Lerp(breathLight.intensity, targetIntensity, Time.deltaTime * 20f);
            }
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
