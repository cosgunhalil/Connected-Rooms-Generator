using UnityEngine;

namespace CRG.Samples.GenerationShowcase
{
    // Orbits the camera around a target: it eases towards each new framing (Frame), circles slowly on its own, and
    // follows mouse drags (orbit) and the scroll wheel (zoom).
    [AddComponentMenu("CRG/Samples/CRG Showcase Orbit Camera")]
    [RequireComponent(typeof(Camera))]
    public sealed class CRGShowcaseOrbitCamera : MonoBehaviour
    {
        [Tooltip("Degrees per second the camera circles on its own.")]
        public float autoOrbitSpeed = 6f;

        [Tooltip("Degrees per pixel of mouse drag.")]
        public float dragSensitivity = 0.25f;

        [Range(10f, 85f)]
        public float pitch = 52f;

        [Tooltip("How quickly the camera reaches a new framing (higher is faster).")]
        public float smoothing = 2.5f;

        private Vector3 target;
        private float distance = 40f;
        private float yaw = 30f;
        private float zoom = 1f;
        private Vector3 currentTarget;
        private float currentDistance = 40f;

        // Frames bounds: the camera's whole view fits a sphere around them.
        public void Frame(Bounds bounds)
        {
            Camera view = GetComponent<Camera>();
            float radius = Mathf.Max(bounds.extents.magnitude, 1f);
            float halfFov = Mathf.Deg2Rad * Mathf.Min(view.fieldOfView, Camera.VerticalToHorizontalFieldOfView(view.fieldOfView, view.aspect)) * 0.5f;
            target = bounds.center;
            distance = radius / Mathf.Sin(halfFov) * 1.05f;
        }

        // Jumps straight to the current framing (for the first level).
        public void Snap()
        {
            currentTarget = target;
            currentDistance = distance;
            Place();
        }

        private void LateUpdate()
        {
            if (ShowcaseInput.Dragging())
            {
                Vector2 delta = ShowcaseInput.MouseDelta();
                yaw += delta.x * dragSensitivity;
                pitch = Mathf.Clamp(pitch - delta.y * dragSensitivity, 10f, 85f);
            }
            else
            {
                yaw += autoOrbitSpeed * Time.deltaTime;
            }
            zoom = Mathf.Clamp(zoom * Mathf.Pow(0.9f, ShowcaseInput.Scroll()), 0.3f, 2.5f);

            float blend = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            currentTarget = Vector3.Lerp(currentTarget, target, blend);
            currentDistance = Mathf.Lerp(currentDistance, distance, blend);
            Place();
        }

        private void Place()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(currentTarget - rotation * Vector3.forward * currentDistance * zoom, rotation);
        }
    }
}
