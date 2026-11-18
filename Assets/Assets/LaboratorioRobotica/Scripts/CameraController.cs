using UnityEngine;

namespace LaboratorioRobotica
{
    /// <summary>
    /// Controlador de cámara interactivo para recorrer el laboratorio en Unity:
    /// - Clic derecho arrastrar: Rotar órbita (Yaw / Pitch)
    /// - Clic central arrastrar o WASD / Flechas: Desplazar (Pan)
    /// - Rueda del ratón: Zoom
    /// - Vistas predefinidas para enfocar cada estación de robot, sensores magnéticos o seguir una bandeja.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        public Vector3 targetPivot = new Vector3(0f, 0.96f, 0f);
        public float distance = 4.6f;
        public float yaw = -128f;
        public float pitch = 30f;

        public Transform followTarget;
        public Vector3 followOffset = new Vector3(0f, 0.08f, 0f);

        private Vector3 _DesiredPivot;
        private float _desiredDistance;
        private float _desiredYaw;
        private float _desiredPitch;

        private void Start()
        {
            _DesiredPivot = targetPivot;
            _desiredDistance = distance;
            _desiredYaw = yaw;
            _desiredPitch = pitch;
            ApplyCameraTransformImmediate();
        }

        public void SetPresetView(Vector3 pivot, float dist, float newYaw, float newPitch, Transform follow = null)
        {
            followTarget = follow;
            _DesiredPivot = pivot;
            _desiredDistance = Mathf.Clamp(dist, 0.35f, 12f);
            _desiredYaw = newYaw;
            _desiredPitch = Mathf.Clamp(newPitch, 5f, 85f);
        }

        private void LateUpdate()
        {
            // Rotación con clic derecho
            if (Input.GetMouseButton(1))
            {
                _desiredYaw += Input.GetAxis("Mouse X") * 3.8f;
                _desiredPitch -= Input.GetAxis("Mouse Y") * 3.2f;
                _desiredPitch = Mathf.Clamp(_desiredPitch, 5f, 86f);
            }

            // Zoom con rueda del ratón (evitando hacer zoom si el cursor está sobre el panel izquierdo de UI)
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f && Input.mousePosition.x > 390f)
            {
                _desiredDistance *= (1f - scroll * 0.85f);
                _desiredDistance = Mathf.Clamp(_desiredDistance, 0.32f, 12f);
            }

            // Paneo con teclado WASD / Flechas o clic central
            Vector3 forwardFlat = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 rightFlat = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

            float moveSpeed = 2.4f * Time.deltaTime;
            if (Input.GetKey(KeyCode.LeftShift)) moveSpeed *= 2.2f;

            Vector3 keyDelta = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) keyDelta += forwardFlat * moveSpeed;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) keyDelta -= forwardFlat * moveSpeed;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) keyDelta += rightFlat * moveSpeed;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) keyDelta -= rightFlat * moveSpeed;

            if (Input.GetMouseButton(2))
            {
                keyDelta -= rightFlat * (Input.GetAxis("Mouse X") * 0.08f * distance);
                keyDelta -= forwardFlat * (Input.GetAxis("Mouse Y") * 0.08f * distance);
            }

            if (keyDelta.sqrMagnitude > 0.000001f)
            {
                followTarget = null;
                _DesiredPivot += keyDelta;
            }

            if (followTarget != null)
            {
                _DesiredPivot = followTarget.position + followOffset;
            }

            float lerpFactor = 1f - Mathf.Exp(-10f * Time.deltaTime);
            targetPivot = Vector3.Lerp(targetPivot, _DesiredPivot, lerpFactor);
            distance = Mathf.Lerp(distance, _desiredDistance, lerpFactor);
            yaw = Mathf.LerpAngle(yaw, _desiredYaw, lerpFactor);
            pitch = Mathf.Lerp(pitch, _desiredPitch, lerpFactor);

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = targetPivot - (rot * Vector3.forward * distance);
            transform.rotation = rot;
        }

        public void ApplyCameraTransformImmediate()
        {
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = targetPivot - (rot * Vector3.forward * distance);
            transform.rotation = rot;
        }
    }
}

