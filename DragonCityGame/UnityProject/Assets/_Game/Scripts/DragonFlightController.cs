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

        [Header("Flight Steering & Aiming")]
        public float pitchRate = 55f;         // Up/Down aiming rotation
        public float yawRate = 50f;           // Left/Right turning
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

        // Internal State
        private Rigidbody rb;
        private float currentSpeed = 0f;
        private float targetSpeed = 0f;
        private float verticalVelocity = 0f;
        private bool isMovingForward = false;
        private bool isHovering = true;

        public float CurrentSpeed => currentSpeed;
        public bool IsHovering => isHovering;

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

            // Check if visual mesh already exists
            Renderer existingRenderer = GetComponentInChildren<Renderer>();
            if (existingRenderer == null)
            {
                BuildProceduralDragonVisual();
            }

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

        private void Update()
        {
            HandleInputs();
            AnimateWings();
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

            // 3. Pitch & Aiming (tilt up/down to aim flame)
            float pitchInput = 0f;
            if (Input.GetKey(KeyCode.T)) pitchInput -= 1f; // Pitch down
            if (Input.GetKey(KeyCode.G)) pitchInput += 1f; // Pitch up

            // Mouse Look / Right click hold for fine aiming or arrow keys
            if (Input.GetMouseButton(1))
            {
                pitchInput -= Input.GetAxis("Mouse Y") * 1.5f;
            }

            // 4. Turning / Yaw (Q/D or A/D or Left/Right arrows)
            float yawInput = 0f;
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) yawInput -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) yawInput += 1f;

            // 5. Manual Roll (keys A/E if configured or Ctrl)
            float rollInput = 0f;

            // Automatic banking into turns only when flying forward
            float autoRoll = (isMovingForward && Mathf.Abs(currentSpeed) > 5f) ? -yawInput * (autoBankStrength * 30f) : 0f;

            // Apply rotations
            float pitchDelta = pitchInput * pitchRate * Time.deltaTime;
            float yawDelta = yawInput * yawRate * Time.deltaTime;
            float rollDelta = rollInput * rollRate * Time.deltaTime;

            transform.Rotate(pitchDelta, yawDelta, rollDelta, Space.Self);

            // Leveling roll correction: restore horizontal wings smoothly
            if (Mathf.Abs(rollInput) < 0.05f)
            {
                Vector3 euler = transform.eulerAngles;
                float currentRoll = (euler.z > 180f) ? euler.z - 360f : euler.z;
                float desiredRoll = Mathf.Clamp(autoRoll, -55f, 55f);
                float smoothedRoll = Mathf.Lerp(currentRoll, desiredRoll, Time.deltaTime * levelRollDamping);
                transform.rotation = Quaternion.Euler(euler.x, euler.y, smoothedRoll);
            }
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

            Material dragonMat = new Material(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Mobile/Diffuse"));
            dragonMat.color = new Color(0.85f, 0.12f, 0.08f); // Draconic Crimson

            Material hornMat = new Material(dragonMat);
            hornMat.color = new Color(0.95f, 0.75f, 0.15f); // Golden Horns/Spines

            // 1. Torso
            CreateVisualPart(PrimitiveType.Cube, "Torso", visualRoot.transform, 
                             new Vector3(0f, 0.5f, 0f), new Vector3(1.6f, 1.2f, 3.0f), dragonMat);

            // 2. Head & Neck
            CreateVisualPart(PrimitiveType.Cube, "Neck", visualRoot.transform,
                             new Vector3(0f, 1.1f, 1.8f), new Vector3(0.9f, 1.0f, 1.4f), dragonMat);
            CreateVisualPart(PrimitiveType.Cube, "Head", visualRoot.transform,
                             new Vector3(0f, 1.4f, 2.7f), new Vector3(1.1f, 0.9f, 1.5f), dragonMat);
            CreateVisualPart(PrimitiveType.Cube, "Snout", visualRoot.transform,
                             new Vector3(0f, 1.2f, 3.7f), new Vector3(0.7f, 0.6f, 1.0f), dragonMat);

            // Horns
            CreateVisualPart(PrimitiveType.Cube, "Horn_L", visualRoot.transform,
                             new Vector3(-0.45f, 2.1f, 2.4f), new Vector3(0.18f, 0.9f, 0.25f), hornMat);
            CreateVisualPart(PrimitiveType.Cube, "Horn_R", visualRoot.transform,
                             new Vector3(0.45f, 2.1f, 2.4f), new Vector3(0.18f, 0.9f, 0.25f), hornMat);

            // 3. Tail
            CreateVisualPart(PrimitiveType.Cube, "Tail_1", visualRoot.transform,
                             new Vector3(0f, 0.5f, -2.0f), new Vector3(1.1f, 0.9f, 1.6f), dragonMat);
            CreateVisualPart(PrimitiveType.Cube, "Tail_2", visualRoot.transform,
                             new Vector3(0f, 0.5f, -3.4f), new Vector3(0.7f, 0.6f, 1.6f), dragonMat);
            CreateVisualPart(PrimitiveType.Cube, "Tail_Fin", visualRoot.transform,
                             new Vector3(0f, 0.7f, -4.6f), new Vector3(0.1f, 1.2f, 1.0f), hornMat);

            // 4. Articulated Left Wing
            GameObject wingL = new GameObject("Wing_L");
            wingL.transform.SetParent(visualRoot.transform, false);
            wingL.transform.localPosition = new Vector3(-0.8f, 0.7f, 0.4f);
            wingLeftBone = wingL.transform;

            CreateVisualPart(PrimitiveType.Cube, "Wing_Arm_L", wingL.transform,
                             new Vector3(-1.8f, 0f, 0f), new Vector3(2.4f, 0.15f, 0.35f), dragonMat);
            CreateVisualPart(PrimitiveType.Cube, "Wing_Membrane_L", wingL.transform,
                             new Vector3(-2.4f, 0f, -0.6f), new Vector3(3.6f, 0.05f, 2.0f), dragonMat);

            // 5. Articulated Right Wing
            GameObject wingR = new GameObject("Wing_R");
            wingR.transform.SetParent(visualRoot.transform, false);
            wingR.transform.localPosition = new Vector3(0.8f, 0.7f, 0.4f);
            wingRightBone = wingR.transform;

            CreateVisualPart(PrimitiveType.Cube, "Wing_Arm_R", wingR.transform,
                             new Vector3(1.8f, 0f, 0f), new Vector3(2.4f, 0.15f, 0.35f), dragonMat);
            CreateVisualPart(PrimitiveType.Cube, "Wing_Membrane_R", wingR.transform,
                             new Vector3(2.4f, 0f, -0.6f), new Vector3(3.6f, 0.05f, 2.0f), dragonMat);

            Debug.Log("[DragonFlight] Built high-visibility procedural dragon geometry with flapping wings.");
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
