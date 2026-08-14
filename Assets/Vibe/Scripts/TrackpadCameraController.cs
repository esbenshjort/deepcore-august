using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepCore.Vibe
{
    /// <summary>
    /// MacBook trackpad-first camera: WASD/arrows pan, Space+drag pan, scroll zoom.
    /// </summary>
    public sealed class TrackpadCameraController : MonoBehaviour
    {
        [SerializeField] float panSpeed = 8f;
        [SerializeField] float dragPanSensitivity = 0.02f;
        [SerializeField] float zoomSpeed = 1.5f;
        [SerializeField] float minZoom = 4f;
        [SerializeField] float maxZoom = 16f;

        Camera _cam;
        bool _dragging;
        Vector2 _lastPointer;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = Camera.main;
        }

        void Update()
        {
            if (_cam == null) return;

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            Vector2 move = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
            }

            if (move.sqrMagnitude > 0f)
            {
                move.Normalize();
                transform.position += (Vector3)(move * panSpeed * Time.unscaledDeltaTime);
            }

            if (mouse != null)
            {
                // Space + primary drag = trackpad pan
                bool space = keyboard != null && keyboard.spaceKey.isPressed;
                if (space && mouse.leftButton.wasPressedThisFrame)
                {
                    _dragging = true;
                    _lastPointer = mouse.position.ReadValue();
                }
                if (mouse.leftButton.wasReleasedThisFrame || (keyboard != null && keyboard.spaceKey.wasReleasedThisFrame))
                    _dragging = false;

                if (_dragging && mouse.leftButton.isPressed)
                {
                    Vector2 pos = mouse.position.ReadValue();
                    Vector2 delta = pos - _lastPointer;
                    _lastPointer = pos;
                    transform.position -= new Vector3(delta.x, delta.y, 0f) * dragPanSensitivity * _cam.orthographicSize;
                }

                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    _cam.orthographicSize = Mathf.Clamp(
                        _cam.orthographicSize - Mathf.Sign(scroll) * zoomSpeed,
                        minZoom,
                        maxZoom);
                }
            }
        }
    }
}
