using UnityEngine;

namespace DragonCity
{
    public class ThirdPersonCamera : MonoBehaviour
    {
        public static ThirdPersonCamera Instance { get; private set; }

        [Header("Target Tracking")]
        public Transform target;
        public Vector3 followOffset = new Vector3(0f, 4.5f, -12f);

        [Header("Smoothing")]
        public float positionSmoothSpeed = 10f;
        public float rotationSmoothSpeed = 8f;
        public float lookAheadDistance = 25f;

        [Header("Dynamic FOV (Speed effect)")]
        public bool enableFovEffect = true;
        public float baseFov = 60f;
        public float maxFov = 78f;
        public float fovTransitionSpeed = 5f;

        [Header("Camera Shake")]
        private float shakeIntensity = 0f;
        private float shakeDuration = 0f;
        private float shakeTimer = 0f;

        private Camera cam;
        private DragonFlightController flightController;

        private void Awake()
        {
            Instance = this;

            cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;

            if (target == null)
            {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
                var player = FindAnyObjectByType<DragonFlightController>();
#else
                var player = FindObjectOfType<DragonFlightController>();
#endif
                if (player != null) target = player.transform;
            }

            if (target != null)
            {
                flightController = target.GetComponent<DragonFlightController>();
            }
        }

        private void Start()
        {
            if (target != null)
            {
                // Snap directly on initial spawn
                Vector3 desiredPos = target.TransformPoint(followOffset);
                transform.position = desiredPos;
                transform.LookAt(target.position + target.forward * lookAheadDistance);
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // 1. Follow Position with local offset
            Vector3 desiredPosition = target.TransformPoint(followOffset);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionSmoothSpeed * Time.deltaTime);

            // 2. Follow Look Rotation towards front of dragon
            Vector3 lookTarget = target.position + (target.forward * lookAheadDistance);
            Vector3 toTarget = (lookTarget - transform.position).normalized;

            if (toTarget != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(toTarget, target.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSmoothSpeed * Time.deltaTime);
            }

            // 3. Dynamic Field of View
            if (enableFovEffect && cam != null && flightController != null)
            {
                float speedRatio = Mathf.Clamp01((flightController.CurrentSpeed - flightController.baseCruiseSpeed) / 
                                                 (flightController.baseCruiseSpeed * flightController.boostMultiplier - flightController.baseCruiseSpeed + 0.001f));
                float desiredFov = Mathf.Lerp(baseFov, maxFov, speedRatio);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, desiredFov, fovTransitionSpeed * Time.deltaTime);
            }

            // 4. Screen Shake for explosions & collapse
            if (shakeTimer > 0f)
            {
                shakeTimer -= Time.deltaTime;
                float currentPower = (shakeTimer / shakeDuration) * shakeIntensity;
                transform.position += Random.insideUnitSphere * currentPower;
            }
        }

        public void TriggerShake(float intensity, float duration)
        {
            shakeIntensity = Mathf.Max(shakeIntensity, intensity);
            shakeDuration = Mathf.Max(shakeDuration, duration);
            shakeTimer = shakeDuration;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
            {
                flightController = target.GetComponent<DragonFlightController>();
            }
        }
    }
}
