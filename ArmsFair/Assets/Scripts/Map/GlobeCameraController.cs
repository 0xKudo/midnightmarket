using UnityEngine;
using UnityEngine.InputSystem;

namespace ArmsFair.Map
{
    // Attach to the globe camera. Drag to orbit, scroll/pinch to zoom.
    public class GlobeCameraController : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float orbitSpeed   = 0.3f;
        [SerializeField] private float zoomSpeed    = 1.0f;
        [SerializeField] private float minDistance  = 6f;
        [SerializeField] private float maxDistance  = 20f;
        [SerializeField] private float autoSpinSpeed = 2f;

        private float   _distance;
        private float   _yaw;
        private float   _pitch;
        private Vector2 _lastMousePos;
        private bool    _dragging;
        private float   _idleTimer;

        private void Start()
        {
            var center = target != null ? target.position : Vector3.zero;
            _distance = (transform.position - center).magnitude;
            _yaw      = transform.eulerAngles.y;
            _pitch    = transform.eulerAngles.x;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            var touch = Touchscreen.current;

            HandleOrbit(mouse, touch);
            HandleZoom(mouse, touch);
            HandleAutoSpin();
            ApplyTransform();
        }

        private void HandleOrbit(Mouse mouse, Touchscreen touch)
        {
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    _lastMousePos = mouse.position.ReadValue();
                    _dragging     = true;
                    _idleTimer    = 0f;
                }
                if (mouse.leftButton.wasReleasedThisFrame)
                    _dragging = false;

                if (_dragging && mouse.leftButton.isPressed)
                {
                    var pos   = mouse.position.ReadValue();
                    var delta = pos - _lastMousePos;
                    _yaw          += delta.x * orbitSpeed;
                    _pitch        -= delta.y * orbitSpeed;
                    _pitch         = Mathf.Clamp(_pitch, -85f, 85f);
                    _lastMousePos  = pos;
                    _idleTimer     = 0f;
                }
            }

            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                var delta = touch.primaryTouch.delta.ReadValue();
                _yaw      += delta.x * orbitSpeed;
                _pitch    -= delta.y * orbitSpeed;
                _pitch     = Mathf.Clamp(_pitch, -85f, 85f);
                _idleTimer = 0f;
            }
        }

        private void HandleZoom(Mouse mouse, Touchscreen touch)
        {
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    _distance -= scroll * zoomSpeed * 0.01f;
                    _distance  = Mathf.Clamp(_distance, minDistance, maxDistance);
                    _idleTimer = 0f;
                }
            }

            if (touch != null && touch.touches.Count >= 2)
            {
                var t0 = touch.touches[0];
                var t1 = touch.touches[1];
                float prevDist = Vector2.Distance(
                    t0.position.ReadValue() - t0.delta.ReadValue(),
                    t1.position.ReadValue() - t1.delta.ReadValue());
                float curDist = Vector2.Distance(
                    t0.position.ReadValue(),
                    t1.position.ReadValue());
                _distance -= (curDist - prevDist) * zoomSpeed * 0.02f;
                _distance  = Mathf.Clamp(_distance, minDistance, maxDistance);
                _idleTimer = 0f;
            }
        }

        private void HandleAutoSpin()
        {
            _idleTimer += Time.deltaTime;
            if (_idleTimer > 3f)
                _yaw += autoSpinSpeed * Time.deltaTime;
        }

        private void ApplyTransform()
        {
            var center = target != null ? target.position : Vector3.zero;
            var rot    = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position = center + rot * new Vector3(0f, 0f, -_distance);
            transform.LookAt(center);
        }
    }
}
