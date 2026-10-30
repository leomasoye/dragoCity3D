using System.Collections.Generic;
using UnityEngine;

namespace DragonCity
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class DragonFlightController : MonoBehaviour
    {
        [Header("Flight Speeds (Hold to Move)")]
        public float baseCruiseSpeed = 36f;
        public float boostMultiplier = 1.9f;
        public float reverseSpeed = 12f;
        public float forwardAcceleration = 28f;
        public float brakingDeceleration = 32f;

        [Header("Hover & Vertical Strafe")]
        public float verticalAscentSpeed = 16f;
        public float hoverBobbingAmplitude = 0.28f;
        public float hoverBobbingFrequency = 1.6f;

        [Header("Mouse Flight Steering")]
        public bool enableMouseSteering = true;
        public float mouseSensitivityX = 2.4f;
        public float mouseSensitivityY = 2.0f;
        public float mouseSmoothing = 18f;
        public bool invertMousePitch = false;
        public float minPitchAngle = -75f;
        public float maxPitchAngle = 75f;
        public float maxBankAngle = 38f;
        public bool autoLockCursor = true;

        [Header("Flight Steering & Aiming")]
        public float pitchRate = 55f;         // Up/Down aiming rotation (keyboard)
        public float yawRate = 50f;           // Left/Right turning (keyboard)
        public float rollRate = 60f;          // Manual banking roll speed
        public float autoBankStrength = 1.2f; // Automatic roll into forward turns
        public float levelRollDamping = 4.0f; // Restores horizon level

        [Header("Collision & Recovery")]
        public float collisionBounceForce = 15f;
        public float collisionStunDuration = 0.4f;
        private float stunTimer = 0f;

        [Header("Visual Wing Flapping")]
        public Transform wingLeftBone;
        public Transform wingRightBone;
        public float cruiseFlapFrequency = 3.6f;
        public float hoverFlapFrequency = 2.2f;
        public float flapAngle = 26f;

        [Header("Rank Evolution & Scaling")]
        public float currentDragonScale = 1.0f;
        public float targetDragonScale = 1.0f;
        public float scaleGrowthSpeed = 1.8f;
        public int currentRank = 0;

        // Visual Materials & Effects
        private Material dragonBodyMat;
        private Material dragonHornMat;
        private List<Renderer> allDragonRenderers = new List<Renderer>();
        private ParticleSystem bodyAuraParticles;
        private ParticleSystem rankBurstParticles;

        // Rank Anatomical Parts
        private List<GameObject> rank1Parts = new List<GameObject>();
        private List<GameObject> rank2Parts = new List<GameObject>();
        private List<GameObject> rank3Parts = new List<GameObject>();
        private List<GameObject> rank4Parts = new List<GameObject>();
        private List<GameObject> rank5Parts = new List<GameObject>();

        private static readonly float[] RANK_SCALES = new float[] { 1.0f, 1.70f, 2.60f, 3.75f, 5.10f, 6.70f };
        private static readonly Color[] RANK_BODY_COLORS = new Color[]
        {
            new Color(0.85f, 0.12f, 0.08f), // Rank 0: Draconic Crimson
            new Color(0.96f, 0.35f, 0.05f), // Rank 1: Amber Solar Flame
            new Color(1.0f, 0.72f, 0.08f),  // Rank 2: Golden Sun Drake
            new Color(0.32f, 0.06f, 0.06f), // Rank 3: Magma Obsidian
            new Color(0.12f, 0.11f, 0.13f), // Rank 4: Ash Charcoal & Brimstone
            new Color(0.18f, 0.03f, 0.28f)  // Rank 5: Cosmic Void Amethyst
        };
        private static readonly Color[] RANK_HORN_COLORS = new Color[]
        {
            new Color(0.95f, 0.75f, 0.15f), // Golden
            new Color(0.98f, 0.52f, 0.12f), // Copper Flare
            new Color(1.0f, 0.95f, 0.55f),  // White-Hot Gold
            new Color(0.95f, 0.22f, 0.18f), // Incandescent Ruby
            new Color(0.85f, 0.05f, 0.12f), // Blood Crimson
            new Color(0.98f, 0.15f, 0.65f)  // Celestial Neon Plasma
        };

        // Internal State
        private Rigidbody rb;
        private float currentSpeed = 0f;
        private float targetSpeed = 0f;
        private float verticalVelocity = 0f;
        private bool isMovingForward = false;
        private bool isHovering = true;

        // Unified flight orientation
        private float currentPitch = 0f;
        private float currentYaw = 0f;
        private float currentRoll = 0f;
        private float smoothMouseX = 0f;
        private float smoothMouseY = 0f;
        private bool isCursorLocked = false;

        public float CurrentSpeed => currentSpeed;
        public bool IsHovering => isHovering;
        public bool IsCursorLocked => isCursorLocked;
        public float CurrentDragonScale => currentDragonScale;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = 1.5f;
            rb.angularDamping = 2.5f;
#else
            rb.drag = 1.5f;
            rb.angularDrag = 2.5f;
#endif
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            currentSpeed = 0f;
            targetSpeed = 0f;

            // Sensible fallbacks if scene deserialized zeroes
            if (mouseSensitivityX <= 0.01f) mouseSensitivityX = 2.4f;
            if (mouseSensitivityY <= 0.01f) mouseSensitivityY = 2.0f;
            if (mouseSmoothing <= 0.1f) mouseSmoothing = 18f;
            if (maxPitchAngle <= 1f) maxPitchAngle = 75f;
            if (minPitchAngle >= -1f) minPitchAngle = -75f;
            if (maxBankAngle <= 1f) maxBankAngle = 38f;

            // Initialize orientation angles from current transform
            Vector3 startEuler = transform.eulerAngles;
            currentYaw = startEuler.y;
            currentPitch = (startEuler.x > 180f) ? startEuler.x - 360f : startEuler.x;
            currentRoll = (startEuler.z > 180f) ? startEuler.z - 360f : startEuler.z;

            // Check if visual mesh already exists
            Renderer existingRenderer = GetComponentInChildren<Renderer>();
            if (existingRenderer == null)
            {
                BuildProceduralDragonVisual();
            }

            // Cache all renderers across the hierarchy
            allDragonRenderers.AddRange(GetComponentsInChildren<Renderer>());

            // Setup Draconic Body Aura & Rank Up Shockwave FX
            SetupDragonAura();
            SetupRankUpBurst();

            // Auto-detect wing bones if not assigned
            if (wingLeftBone == null)
            {
                Transform found = FindDeepChild(transform, "Wing_L");
                if (found != null) wingLeftBone = found;
            }
            if (wingRightBone == null)
            {
                Transform found = FindDeepChild(transform, "Wing_R");
                if (found != null) wingRightBone = found;
            }
        }

        private void Start()
        {
            if (autoLockCursor)
            {
                SetCursorLock(true);
            }
        }

        private void Update()
        {
            HandleCursorLock();
            HandleInputs();
            AnimateWings();
            UpdateScaleGrowth();
        }

        private void UpdateScaleGrowth()
        {
            if (Mathf.Abs(currentDragonScale - targetDragonScale) > 0.001f)
            {
                currentDragonScale = Mathf.MoveTowards(currentDragonScale, targetDragonScale, Time.deltaTime * scaleGrowthSpeed);
                transform.localScale = Vector3.one * currentDragonScale;
                ThirdPersonCamera.Instance?.SetDragonScaleMultiplier(currentDragonScale);
            }
        }

        private void HandleCursorLock()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SetCursorLock(false);
            }
            else if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                if (!isCursorLocked)
                {
                    SetCursorLock(true);
                }
            }
        }

        public void SetCursorLock(bool locked)
        {
            isCursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void FixedUpdate()
        {
            if (stunTimer > 0f)
            {
                stunTimer -= Time.fixedDeltaTime;
                return;
            }

            ApplyFlightPhysics();
        }

        private void HandleInputs()
        {
            // 1. Throttle: Must PRESS & HOLD to move forward (Z / W / UpArrow)
            isMovingForward = Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            bool isMovingBackward = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            bool isBoosting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (isMovingForward)
            {
                isHovering = false;
                targetSpeed = isBoosting ? baseCruiseSpeed * boostMultiplier : baseCruiseSpeed;
            }
            else if (isMovingBackward)
            {
                // Reverse flight backwards slowly
                isHovering = false;
                targetSpeed = -reverseSpeed;
            }
            else
            {
                // RELEASED forward key: automatically decelerate to full stop (hover)
                targetSpeed = 0f;
                if (Mathf.Abs(currentSpeed) < 1.0f)
                {
                    isHovering = true;
                }
            }

            // 2. Vertical Altitude (R to ascend, F to descend)
            verticalVelocity = 0f;
            if (Input.GetKey(KeyCode.R)) verticalVelocity += verticalAscentSpeed;
            if (Input.GetKey(KeyCode.F)) verticalVelocity -= verticalAscentSpeed;

            // 3. Mouse Flight Steering
            float mouseDeltaX = 0f;
            float mouseDeltaY = 0f;
            if (enableMouseSteering && isCursorLocked)
            {
                float targetMouseX = Input.GetAxis("Mouse X") * mouseSensitivityX;
                float targetMouseY = Input.GetAxis("Mouse Y") * mouseSensitivityY;
                smoothMouseX = Mathf.Lerp(smoothMouseX, targetMouseX, Time.deltaTime * mouseSmoothing);
                smoothMouseY = Mathf.Lerp(smoothMouseY, targetMouseY, Time.deltaTime * mouseSmoothing);
                mouseDeltaX = smoothMouseX;
                mouseDeltaY = smoothMouseY;
            }

            // 4. Keyboard Pitch & Aiming (tilt up/down to aim flame)
            float keyPitch = 0f;
            if (Input.GetKey(KeyCode.T)) keyPitch += 1f; // Pitch down
            if (Input.GetKey(KeyCode.G)) keyPitch -= 1f; // Pitch up

            // 5. Keyboard Turning / Yaw (Q/D or A/D or Left/Right arrows)
            float keyYaw = 0f;
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) keyYaw -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) keyYaw += 1f;

            // Combine Inputs:
            // Yaw turns left/right
            float yawDelta = (keyYaw * yawRate * Time.deltaTime) + mouseDeltaX;
            currentYaw += yawDelta;

            // Pitch climbs/dives: in Unity, negative pitch tilts nose up, positive pitch tilts nose down
            float pitchDir = invertMousePitch ? 1f : -1f;
            float pitchDelta = (keyPitch * pitchRate * Time.deltaTime) + (mouseDeltaY * pitchDir);
            currentPitch = Mathf.Clamp(currentPitch + pitchDelta, minPitchAngle, maxPitchAngle);

            // 6. Dynamic Banking Roll into turns
            float turnRate = yawDelta / Mathf.Max(0.0001f, Time.deltaTime);
            float targetRoll = 0f;
            if (Mathf.Abs(turnRate) > 0.05f)
            {
                targetRoll = -Mathf.Clamp(turnRate * autoBankStrength * 0.22f, -maxBankAngle, maxBankAngle);
            }
            currentRoll = Mathf.Lerp(currentRoll, targetRoll, Time.deltaTime * levelRollDamping);

            // Apply unified, gimbal-safe rotation
            transform.rotation = Quaternion.Euler(currentPitch, currentYaw, currentRoll);
        }

        private void ApplyFlightPhysics()
        {
            // Decelerate smoothly to 0 when forward key is released
            float rate = (Mathf.Abs(targetSpeed) < Mathf.Abs(currentSpeed)) ? brakingDeceleration : forwardAcceleration;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * Time.fixedDeltaTime);

            Vector3 flightVelocity;

            if (isHovering)
            {
                // Gentle floating breath bobbing when stationary
                float bobbing = Mathf.Sin(Time.time * hoverBobbingFrequency * Mathf.PI * 2f) * hoverBobbingAmplitude;
                flightVelocity = new Vector3(0f, verticalVelocity + bobbing, 0f);
            }
            else
            {
                // Forward (or reverse) vector
                flightVelocity = (transform.forward * currentSpeed) + new Vector3(0f, verticalVelocity, 0f);
            }

#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = flightVelocity;
            rb.angularVelocity = Vector3.zero;
#else
            rb.velocity = flightVelocity;
            rb.angularVelocity = Vector3.zero;
#endif
        }

        private void AnimateWings()
        {
            float flapFreq;
            float currentFlapAngle;

            if (isHovering || Mathf.Abs(currentSpeed) < 3f)
            {
                // Soft, continuous hover flapping
                flapFreq = hoverFlapFrequency;
                currentFlapAngle = flapAngle * 1.25f;
            }
            else
            {
                // Faster flapping when actively moving forward
                float speedRatio = Mathf.Abs(currentSpeed) / baseCruiseSpeed;
                flapFreq = cruiseFlapFrequency * Mathf.Max(0.7f, speedRatio);
                currentFlapAngle = flapAngle;
            }

            float flapSin = Mathf.Sin(Time.time * flapFreq * Mathf.PI * 2f);
            float currentFlap = flapSin * currentFlapAngle;

            if (wingLeftBone != null)
            {
                wingLeftBone.localRotation = Quaternion.Euler(0f, 0f, currentFlap);
            }
            if (wingRightBone != null)
            {
                wingRightBone.localRotation = Quaternion.Euler(0f, 0f, -currentFlap);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            stunTimer = collisionStunDuration;

            Vector3 normal = collision.contacts.Length > 0 ? collision.contacts[0].normal : -transform.forward;
            Vector3 bounceDirection = (normal + Vector3.up * 0.4f).normalized;

#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = bounceDirection * collisionBounceForce;
#else
            rb.velocity = bounceDirection * collisionBounceForce;
#endif
            currentSpeed = 0f;
            targetSpeed = 0f;
            isHovering = true;

            transform.position += bounceDirection * 1.5f;

            // Re-sync flight orientation angles after bounce
            Vector3 fwd = transform.forward;
            currentYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            currentPitch = -Mathf.Asin(Mathf.Clamp(fwd.y, -0.99f, 0.99f)) * Mathf.Rad2Deg;
            currentRoll = 0f;

            Debug.Log($"[DragonFlight] Impact with {collision.gameObject.name}! Rebounding into hover mode.");
        }

        /// <summary>
        /// Automatically generates a stylized dragon visual hierarchy if no external mesh was attached.
        /// </summary>
        private void BuildProceduralDragonVisual()
        {
            GameObject visualRoot = new GameObject("Dragon_Visual_Model");
            visualRoot.transform.SetParent(transform, false);
            visualRoot.transform.localPosition = Vector3.zero;
            visualRoot.transform.localRotation = Quaternion.identity;

            dragonBodyMat = new Material(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Mobile/Diffuse"));
            dragonBodyMat.color = RANK_BODY_COLORS[0];

            dragonHornMat = new Material(dragonBodyMat);
            dragonHornMat.color = RANK_HORN_COLORS[0];

            // 1. Torso
            CreateVisualPart(PrimitiveType.Cube, "Torso", visualRoot.transform, 
                             new Vector3(0f, 0.5f, 0f), new Vector3(1.6f, 1.2f, 3.0f), dragonBodyMat);

            // 2. Head & Neck
            CreateVisualPart(PrimitiveType.Cube, "Neck", visualRoot.transform,
                             new Vector3(0f, 1.1f, 1.8f), new Vector3(0.9f, 1.0f, 1.4f), dragonBodyMat);
            CreateVisualPart(PrimitiveType.Cube, "Head", visualRoot.transform,
                             new Vector3(0f, 1.4f, 2.7f), new Vector3(1.1f, 0.9f, 1.5f), dragonBodyMat);
            CreateVisualPart(PrimitiveType.Cube, "Snout", visualRoot.transform,
                             new Vector3(0f, 1.2f, 3.7f), new Vector3(0.7f, 0.6f, 1.0f), dragonBodyMat);

            // Horns
            CreateVisualPart(PrimitiveType.Cube, "Horn_L", visualRoot.transform,
                             new Vector3(-0.45f, 2.1f, 2.4f), new Vector3(0.18f, 0.9f, 0.25f), dragonHornMat);
            CreateVisualPart(PrimitiveType.Cube, "Horn_R", visualRoot.transform,
                             new Vector3(0.45f, 2.1f, 2.4f), new Vector3(0.18f, 0.9f, 0.25f), dragonHornMat);

            // 3. Tail
            CreateVisualPart(PrimitiveType.Cube, "Tail_1", visualRoot.transform,
                             new Vector3(0f, 0.5f, -2.0f), new Vector3(1.1f, 0.9f, 1.6f), dragonBodyMat);
            CreateVisualPart(PrimitiveType.Cube, "Tail_2", visualRoot.transform,
                             new Vector3(0f, 0.5f, -3.4f), new Vector3(0.7f, 0.6f, 1.6f), dragonBodyMat);
            CreateVisualPart(PrimitiveType.Cube, "Tail_Fin", visualRoot.transform,
                             new Vector3(0f, 0.7f, -4.6f), new Vector3(0.1f, 1.2f, 1.0f), dragonHornMat);

            // 4. Articulated Left Wing
            GameObject wingL = new GameObject("Wing_L");
            wingL.transform.SetParent(visualRoot.transform, false);
            wingL.transform.localPosition = new Vector3(-0.8f, 0.7f, 0.4f);
            wingLeftBone = wingL.transform;

            CreateVisualPart(PrimitiveType.Cube, "Wing_Arm_L", wingL.transform,
                             new Vector3(-1.8f, 0f, 0f), new Vector3(2.4f, 0.15f, 0.35f), dragonBodyMat);
            CreateVisualPart(PrimitiveType.Cube, "Wing_Membrane_L", wingL.transform,
                             new Vector3(-2.4f, 0f, -0.6f), new Vector3(3.6f, 0.05f, 2.0f), dragonBodyMat);

            // 5. Articulated Right Wing
            GameObject wingR = new GameObject("Wing_R");
            wingR.transform.SetParent(visualRoot.transform, false);
            wingR.transform.localPosition = new Vector3(0.8f, 0.7f, 0.4f);
            wingRightBone = wingR.transform;

            CreateVisualPart(PrimitiveType.Cube, "Wing_Arm_R", wingR.transform,
                             new Vector3(1.8f, 0f, 0f), new Vector3(2.4f, 0.15f, 0.35f), dragonBodyMat);
            CreateVisualPart(PrimitiveType.Cube, "Wing_Membrane_R", wingR.transform,
                             new Vector3(2.4f, 0f, -0.6f), new Vector3(3.6f, 0.05f, 2.0f), dragonBodyMat);

            // ========================================================
            // DISTINCT ANATOMICAL EVOLUTION ADD-ONS PER RANK (BEYOND COLOR)
            // ========================================================
            // 6. Rank 1 Addons: Jagged Dorsal Spines along neck and back
            rank1Parts.Add(CreateVisualPart(PrimitiveType.Cube, "Dorsal_Spine_1", visualRoot.transform, new Vector3(0f, 1.8f, 1.8f), new Vector3(0.18f, 0.6f, 0.35f), dragonHornMat));
            rank1Parts.Add(CreateVisualPart(PrimitiveType.Cube, "Dorsal_Spine_2", visualRoot.transform, new Vector3(0f, 1.4f, 0.8f), new Vector3(0.18f, 0.7f, 0.45f), dragonHornMat));
            rank1Parts.Add(CreateVisualPart(PrimitiveType.Cube, "Dorsal_Spine_3", visualRoot.transform, new Vector3(0f, 1.3f, -0.4f), new Vector3(0.18f, 0.75f, 0.5f), dragonHornMat));
            rank1Parts.Add(CreateVisualPart(PrimitiveType.Cube, "Dorsal_Spine_4", visualRoot.transform, new Vector3(0f, 1.1f, -1.5f), new Vector3(0.18f, 0.65f, 0.45f), dragonHornMat));

            // 7. Rank 2 Addons: Giant Secondary Ram Horns & Temple Crests
            rank2Parts.Add(CreateVisualPart(PrimitiveType.Cube, "Ram_Horn_L", visualRoot.transform, new Vector3(-0.75f, 1.6f, 2.3f), new Vector3(0.25f, 0.65f, 0.7f), dragonHornMat));
            rank2Parts.Add(CreateVisualPart(PrimitiveType.Cube, "Ram_Horn_R", visualRoot.transform, new Vector3(0.75f, 1.6f, 2.3f), new Vector3(0.25f, 0.65f, 0.7f), dragonHornMat));
            rank2Parts.Add(CreateVisualPart(PrimitiveType.Cube, "Jaw_Spike_L", visualRoot.transform, new Vector3(-0.65f, 0.9f, 2.9f), new Vector3(0.18f, 0.35f, 0.55f), dragonHornMat));
            rank2Parts.Add(CreateVisualPart(PrimitiveType.Cube, "Jaw_Spike_R", visualRoot.transform, new Vector3(0.65f, 0.9f, 2.9f), new Vector3(0.18f, 0.35f, 0.55f), dragonHornMat));

            // 8. Rank 3 Addons: Armored Shoulder Pauldrons & Heavy Chest Carapace
            rank3Parts.Add(CreateVisualPart(PrimitiveType.Cube, "Shoulder_Plate_L", visualRoot.transform, new Vector3(-1.15f, 1.0f, 0.3f), new Vector3(0.6f, 0.65f, 1.3f), dragonBodyMat));
            rank3Parts.Add(CreateVisualPart(PrimitiveType.Cube, "Shoulder_Plate_R", visualRoot.transform, new Vector3(1.15f, 1.0f, 0.3f), new Vector3(0.6f, 0.65f, 1.3f), dragonBodyMat));
            rank3Parts.Add(CreateVisualPart(PrimitiveType.Cube, "Chest_Plates", visualRoot.transform, new Vector3(0f, -0.15f, 0.8f), new Vector3(1.4f, 0.45f, 1.8f), dragonHornMat));

            // 9. Rank 4 Addons: Spiked War-Mace Club & Razor Tail Fins
            rank4Parts.Add(CreateVisualPart(PrimitiveType.Cube, "Tail_Mace_Core", visualRoot.transform, new Vector3(0f, 0.7f, -4.9f), new Vector3(0.9f, 0.9f, 1.3f), dragonBodyMat));
            rank4Parts.Add(CreateVisualPart(PrimitiveType.Cube, "Tail_Spike_L", visualRoot.transform, new Vector3(-0.75f, 0.7f, -4.9f), new Vector3(0.65f, 0.2f, 0.4f), dragonHornMat));
            rank4Parts.Add(CreateVisualPart(PrimitiveType.Cube, "Tail_Spike_R", visualRoot.transform, new Vector3(0.75f, 0.7f, -4.9f), new Vector3(0.65f, 0.2f, 0.4f), dragonHornMat));
            rank4Parts.Add(CreateVisualPart(PrimitiveType.Cube, "Tail_Spike_Top", visualRoot.transform, new Vector3(0f, 1.45f, -4.9f), new Vector3(0.2f, 0.65f, 0.4f), dragonHornMat));

            // 10. Rank 5 Addons: Apocalyptic Dread Wing Blades & Celestial Crown
            rank5Parts.Add(CreateVisualPart(PrimitiveType.Cube, "Wing_Blade_L", wingL.transform, new Vector3(-4.0f, 0f, 0.3f), new Vector3(2.2f, 0.08f, 0.65f), dragonHornMat));
            rank5Parts.Add(CreateVisualPart(PrimitiveType.Cube, "Wing_Blade_R", wingR.transform, new Vector3(4.0f, 0f, 0.3f), new Vector3(2.2f, 0.08f, 0.65f), dragonHornMat));
            rank5Parts.Add(CreateVisualPart(PrimitiveType.Cube, "Crown_Spike_Center", visualRoot.transform, new Vector3(0f, 2.9f, 2.0f), new Vector3(0.22f, 1.4f, 0.35f), dragonHornMat));
            rank5Parts.Add(CreateVisualPart(PrimitiveType.Cube, "Crown_Spike_L", visualRoot.transform, new Vector3(-0.4f, 2.6f, 1.9f), new Vector3(0.2f, 1.15f, 0.3f), dragonHornMat));
            rank5Parts.Add(CreateVisualPart(PrimitiveType.Cube, "Crown_Spike_R", visualRoot.transform, new Vector3(0.4f, 2.6f, 1.9f), new Vector3(0.2f, 1.15f, 0.3f), dragonHornMat));

            // Cache newly created renderers
            allDragonRenderers.Clear();
            allDragonRenderers.AddRange(GetComponentsInChildren<Renderer>());

            // Apply anatomical state for current rank
            ApplyRankEvolution(currentRank);

            Debug.Log("[DragonFlight] Built high-visibility procedural dragon geometry with evolving rank anatomy.");
        }

        private void SetupDragonAura()
        {
            // Discreet tail-tip ember trail (drifts behind the dragon, 100% clean forward visibility)
            GameObject auraObj = new GameObject("DragonTailEmbers");
            auraObj.transform.SetParent(transform, false);
            auraObj.transform.localPosition = new Vector3(0f, 0.5f, -4.5f);
            auraObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            bodyAuraParticles = auraObj.AddComponent<ParticleSystem>();
            var psRend = auraObj.GetComponent<ParticleSystemRenderer>();

            var main = bodyAuraParticles.main;
            main.playOnAwake = true;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.0f, 9.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.startColor = new Color(1f, 0.6f, 0.15f, 0.8f);

            var emission = bodyAuraParticles.emission;
            emission.rateOverTime = 10f; // Very low & clean

            var shape = bodyAuraParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.25f;

            Shader partShader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Mobile/Particles/Additive");
            if (partShader != null)
            {
                psRend.material = new Material(partShader);
            }
        }

        private void SetupRankUpBurst()
        {
            GameObject burstObj = new GameObject("RankUpBurstFX");
            burstObj.transform.SetParent(transform, false);
            burstObj.transform.localPosition = Vector3.zero;

            rankBurstParticles = burstObj.AddComponent<ParticleSystem>();
            var psRend = burstObj.GetComponent<ParticleSystemRenderer>();

            var main = rankBurstParticles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(12f, 26f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            main.startColor = new Color(1f, 0.8f, 0.2f, 0.95f);

            var emission = rankBurstParticles.emission;
            emission.rateOverTime = 0;

            var shape = rankBurstParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.2f;

            Shader partShader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Mobile/Particles/Additive");
            if (partShader != null)
            {
                psRend.material = new Material(partShader);
            }
        }

        public void ApplyRankEvolution(int rankIndex)
        {
            rankIndex = Mathf.Clamp(rankIndex, 0, RANK_SCALES.Length - 1);
            targetDragonScale = RANK_SCALES[rankIndex];

            if (rankIndex > currentRank)
            {
                TriggerRankUpFX(rankIndex);
            }

            currentRank = rankIndex;

            // 1. Update dragon materials & colors
            Color bodyCol = RANK_BODY_COLORS[rankIndex];
            Color hornCol = RANK_HORN_COLORS[rankIndex];

            if (dragonBodyMat != null) dragonBodyMat.color = bodyCol;
            if (dragonHornMat != null) dragonHornMat.color = hornCol;

            foreach (var r in allDragonRenderers)
            {
                if (r == null || r.material == null) continue;
                if (r.name.Contains("Horn") || r.name.Contains("Fin") || r.name.Contains("Spine") || r.name.Contains("Blade") || r.name.Contains("Mace"))
                {
                    r.material.color = hornCol;
                }
                else
                {
                    r.material.color = bodyCol;
                }
            }

            // 2. Physical Anatomical Evolution (spines, ram horns, armor, tail mace, wing blades)
            SetPartsActive(rank1Parts, rankIndex >= 1, hornCol);
            SetPartsActive(rank2Parts, rankIndex >= 2, hornCol);
            SetPartsActive(rank3Parts, rankIndex >= 3, bodyCol);
            SetPartsActive(rank4Parts, rankIndex >= 4, hornCol);
            SetPartsActive(rank5Parts, rankIndex >= 5, hornCol);

            // 3. Update clean tail ember trail
            UpdateDragonAura(rankIndex);

            // 4. Deepen wing flap frequency for immense presence
            cruiseFlapFrequency = Mathf.Lerp(3.6f, 1.9f, rankIndex / 5f);
            flapAngle = Mathf.Lerp(26f, 34f, rankIndex / 5f);
        }

        private void SetPartsActive(List<GameObject> parts, bool active, Color col)
        {
            foreach (var p in parts)
            {
                if (p == null) continue;
                p.SetActive(active);
                if (active)
                {
                    Renderer r = p.GetComponent<Renderer>();
                    if (r != null && r.material != null) r.material.color = col;
                }
            }
        }

        private void UpdateDragonAura(int rankIndex)
        {
            if (bodyAuraParticles == null) return;

            var main = bodyAuraParticles.main;
            var emission = bodyAuraParticles.emission;

            // Clean, non-intrusive trail count
            float[] auraRates = new float[] { 8f, 12f, 16f, 20f, 24f, 28f };
            emission.rateOverTime = auraRates[rankIndex];

            Color auraCol = RANK_HORN_COLORS[rankIndex];
            auraCol.a = 0.8f;
            main.startColor = auraCol;
        }

        private void TriggerRankUpFX(int rankIndex)
        {
            if (rankBurstParticles != null)
            {
                var main = rankBurstParticles.main;
                main.startColor = RANK_HORN_COLORS[rankIndex];
                rankBurstParticles.Emit(50);
            }

            if (ThirdPersonCamera.Instance != null)
            {
                ThirdPersonCamera.Instance.TriggerShake(1.25f, 0.75f);
            }

            Debug.Log($"[DragonFlight] Dragon evolved to Rank {rankIndex}! Target Scale: {RANK_SCALES[rankIndex]}x");
        }

        private GameObject CreateVisualPart(PrimitiveType type, string partName, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;

            Collider col = part.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            Renderer rend = part.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = mat;

            return part;
        }

        private Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Equals(childName, System.StringComparison.OrdinalIgnoreCase))
                    return child;

                Transform sub = FindDeepChild(child, childName);
                if (sub != null) return sub;
            }
            return null;
        }
    }
}
