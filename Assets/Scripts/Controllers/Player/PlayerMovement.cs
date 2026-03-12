// =============================================================================
// PlayerMovement.cs - 플레이어 물리 이동 / 카메라 시점 / 스팟라이트 / 입력
// =============================================================================
using UnityEngine;
using BioBreach.Engine.Inventory;

namespace BioBreach.Controller.Player
{
    public class PlayerMovement : MonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("이동")]
        public float gravityMultiplier = 2f;

        [Header("카메라")]
        public float mouseSensitivity = 2f;
        public float minPitch = -80f;
        public float maxPitch =  80f;

        [Header("Ground Check")]
        public LayerMask groundLayer = ~0;

        [Header("스팟라이트")]
        public float lightFollowSpeed = 6f;
        public float lightSpotAngle   = 60f;
        public float lightRange       = 30f;
        public float lightIntensity   = 1f;

        // =====================================================================
        // 런타임 참조 (PlayerController.OnNetworkSpawn 에서 Init 으로 주입)
        // =====================================================================

        private CharacterController _controller;
        private Camera              _camera;
        private Light               _spotLight;
        private PlayerInventory     _inventory;

        // =====================================================================
        // 내부 상태
        // =====================================================================

        private Vector3 _velocity;
        private bool    _isGrounded;
        private float   _pitch;

        /// <summary>현재 카메라 피치 (PlayerController 가 NetworkVariable 에 기록)</summary>
        public float Pitch => _pitch;

        // =====================================================================
        // 초기화
        // =====================================================================

        public void Init(CharacterController controller, Camera camera, Light spotLight, PlayerInventory inventory)
        {
            _controller = controller;
            _camera     = camera;
            _spotLight  = spotLight;
            _inventory  = inventory;

            _spotLight.spotAngle = lightSpotAngle;
            _spotLight.range     = lightRange;
            _spotLight.intensity = lightIntensity;
        }

        // =====================================================================
        // 소유자 프레임 틱
        // =====================================================================

        /// <summary>매 Update 에서 PlayerController 가 호출한다.</summary>
        public void Tick(float moveSpeed, float jumpHeight)
        {
            HandleCursorLock();
            HandleMouseLook();
            HandleMovement(moveSpeed, jumpHeight);
            HandleSlotSelection();
            UpdateSpotLight();
        }

        // =====================================================================
        // 마우스 시점
        // =====================================================================

        void HandleMouseLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            transform.Rotate(Vector3.up * mouseX);
            _pitch = Mathf.Clamp(_pitch - mouseY, minPitch, maxPitch);
            _camera.transform.localRotation = Quaternion.Euler(_pitch, 0, 0);
        }

        // =====================================================================
        // 이동
        // =====================================================================

        void HandleMovement(float moveSpeed, float jumpHeight)
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");

            _controller.Move((transform.right * h + transform.forward * v) * moveSpeed * Time.deltaTime);

            _velocity.y += Physics.gravity.y * gravityMultiplier * Time.deltaTime;
            _velocity.y  = Mathf.Max(_velocity.y, -50f);
            _controller.Move(_velocity * Time.deltaTime);

            CheckGrounded();

            if (_isGrounded)
            {
                if (_velocity.y < 0f) _velocity.y = -2f;
                if (Input.GetKeyDown(KeyCode.Space))
                    _velocity.y = Mathf.Sqrt(jumpHeight * 2f * Mathf.Abs(Physics.gravity.y));
            }
        }

        void CheckGrounded()
        {
            if (_controller.isGrounded) { _isGrounded = true; return; }

            float   radius       = _controller.radius * 0.9f;
            Vector3 sphereOrigin = transform.position + _controller.center
                                   + Vector3.down * (_controller.height * 0.5f - _controller.radius);
            float checkDist = _controller.skinWidth + 0.05f;

            _isGrounded = Physics.SphereCast(
                sphereOrigin, radius, Vector3.down,
                out RaycastHit _, checkDist, groundLayer,
                QueryTriggerInteraction.Ignore);
        }

        // =====================================================================
        // 핫바 슬롯 선택
        // =====================================================================

        void HandleSlotSelection()
        {
            for (int i = 0; i < _inventory.hotbarSize; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    _inventory.SelectSlot(i);

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if      (scroll > 0) _inventory.SelectPrevious();
            else if (scroll < 0) _inventory.SelectNext();
        }

        // =====================================================================
        // 스팟라이트
        // =====================================================================

        void UpdateSpotLight()
        {
            if (_spotLight == null || _camera == null) return;
            _spotLight.transform.SetPositionAndRotation(
                _camera.transform.position,
                Quaternion.Slerp(
                    _spotLight.transform.rotation,
                    _camera.transform.rotation,
                    lightFollowSpeed * Time.deltaTime
                )
            );
        }

        // =====================================================================
        // 커서 잠금
        // =====================================================================

        void HandleCursorLock()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible   = true;
            }
            else if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible   = false;
            }
        }
    }
}
