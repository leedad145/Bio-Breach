// =============================================================================
// PlayerController.cs - 1인칭 플레이어 컨트롤러
// =============================================================================
using UnityEngine;
using BioBreach.Engine.Inventory;
using BioBreach.Core.Voxel;
using BioBreach.Core.Item;
using BioBreach.Systems;
using BioBreach.Engine.Item;

namespace BioBreach.Controller.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInventory))]
    [RequireComponent(typeof(PlacementPreview))]
    public class PlayerController : MonoBehaviour, IPlayerContext
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("이동")]
        public float moveSpeed        = 10f;
        public float jumpHeight       = 3f;
        public float gravityMultiplier = 2f;

        [Header("카메라")]
        public float mouseSensitivity = 2f;
        public float minPitch = -80f;
        public float maxPitch =  80f;

        [Header("파기 (VoxelBlock)")]
        public float interactDistance = 20f;

        [Header("설치 (Placeable)")]
        public float placeNormalOffset = 0.05f;

        [Header("쿨다운")]
        public float actionCooldown = 0.1f;

        [Header("Ground Check")]
        public LayerMask groundLayer = ~0;

        [Header("참조")]
        public WorldManager worldManager;

        [Header("스팟라이트")]
        public float lightFollowSpeed = 6f;
        public float lightSpotAngle   = 60f;
        public float lightRange       = 30f;
        public float lightIntensity   = 1f;

        [Header("디버그")]
        public bool showDebugUI = true;

        // =====================================================================
        // 내부 변수
        // =====================================================================

        private CharacterController _controller;
        private PlayerInventory     _inventory;
        private PlacementPreview    _preview;
        private Camera              _camera;
        private Light               _spotLight;

        private Vector3 _velocity;
        private float   _pitch;
        private bool    _isGrounded;

        private float _lastActionTime = -999f;

        // 매 프레임 캐시
        private bool       _cachedHitValid;
        private RaycastHit _cachedHit;
        private bool       _primaryDown, _primaryHeld, _secondaryDown, _secondaryHeld;

        private GameObject   _lastPreviewPrefab;
        private ItemInstance _lastBoundItem; // 마지막으로 BindToPlayer한 인스턴스

        public bool UIBlocked { get; set; } = false;

        // =====================================================================
        // IPlayerContext 구현
        // =====================================================================

        public PlayerInventory Inventory       => _inventory;
        public float           PlaceNormalOffset => placeNormalOffset;
        public bool            HasHit          => _cachedHitValid;
        public RaycastHit      Hit             => _cachedHit;
        public Vector3         AttackOrigin    => _camera != null ? _camera.transform.position : transform.position;
        public Vector3         AttackDirection => _camera != null ? _camera.transform.forward  : transform.forward;
        public bool            PrimaryDown     => _primaryDown;
        public bool            PrimaryHeld     => _primaryHeld;
        public bool            SecondaryDown   => _secondaryDown;
        public bool            SecondaryHeld   => _secondaryHeld;

        public bool CanPlaceAt(Vector3 pos)
            => Vector3.Distance(pos, transform.position) > _controller.radius + 0.5f;

        public VoxelType GetVoxelTypeAt(Vector3 worldPos)
        {
            if (worldManager == null) return VoxelType.Air;
            foreach (var kvp in worldManager.ActiveChunks)
            {
                var chunk = kvp.Value;
                if (chunk.ContainsPoint(worldPos))
                    return chunk.GetVoxelTypeAt(worldPos);
            }
            return VoxelType.Air;
        }

        public float ModifyTerrain(Vector3 pos, float radius, float strength, VoxelType type)
            => worldManager != null ? worldManager.ModifyTerrain(pos, radius, strength, type) : 0f;

        public void AddMoveSpeed(float v)  => moveSpeed  += v;
        public void AddJumpHeight(float v) => jumpHeight += v;

        // =====================================================================
        // 초기화
        // =====================================================================

        void Start()
        {
            _controller = GetComponent<CharacterController>();
            _inventory  = GetComponent<PlayerInventory>();
            _preview    = GetComponent<PlacementPreview>();

            _camera = GetComponentInChildren<Camera>();
            if (_camera == null)
            {
                var camObj = new GameObject("PlayerCamera");
                camObj.transform.SetParent(transform);
                camObj.transform.localPosition = new Vector3(0, 0.6f, 0);
                _camera = camObj.AddComponent<Camera>();
            }

            _spotLight = GetComponentInChildren<Light>();
            if (_spotLight == null)
            {
                var lightObj = new GameObject("PlayerSpotLight");
                lightObj.transform.SetParent(transform);
                lightObj.transform.localPosition = new Vector3(0, 0.6f, 0);
                _spotLight = lightObj.AddComponent<Light>();
                _spotLight.type = LightType.Spot;
            }
            _spotLight.spotAngle = lightSpotAngle;
            _spotLight.range     = lightRange;
            _spotLight.intensity = lightIntensity;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        // =====================================================================
        // 업데이트
        // =====================================================================

        void Update()
        {
            if (UIBlocked)
            {
                _preview.Hide();
                return;
            }

            HandleMouseLook();
            HandleMovement();
            HandleSlotSelection();
            CacheRaycast();
            CacheInput();
            UpdatePlacementPreview();
            HandleAction();
            HandleCursorLock();
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
        // 이동
        // =====================================================================

        void HandleMovement()
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
        // 슬롯 선택
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
        // 캐시 (매 프레임 1회)
        // =====================================================================

        void CacheRaycast()
        {
            _cachedHitValid = false;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            if (Physics.Raycast(ray, out _cachedHit, interactDistance))
                _cachedHitValid = true;
        }

        void CacheInput()
        {
            _primaryDown   = Input.GetMouseButtonDown(0);
            _primaryHeld   = Input.GetMouseButton(0);
            _secondaryDown = Input.GetMouseButtonDown(1);
            _secondaryHeld = Input.GetMouseButton(1);
        }

        // =====================================================================
        // 설치 미리보기
        // =====================================================================

        void UpdatePlacementPreview()
        {
            var selected = _inventory.SelectedItem;

            if (selected == null || selected.data.category != ItemCategory.Placeable
                || selected.data.placeablePrefab == null)
            {
                _preview.Hide();
                _lastPreviewPrefab = null;
                return;
            }

            var prefab = selected.data.previewPrefab != null
                ? selected.data.previewPrefab
                : selected.data.placeablePrefab;

            if (prefab != _lastPreviewPrefab)
            {
                _preview.Initialize(prefab);
                _lastPreviewPrefab = prefab;
            }

            if (!_cachedHitValid) { _preview.Hide(); return; }

            Vector3    pos      = _cachedHit.point + _cachedHit.normal * placeNormalOffset;
            Quaternion rot      = Quaternion.FromToRotation(Vector3.up, _cachedHit.normal);

            _preview.UpdatePose(pos, rot, CanPlaceAt(pos));
        }

        // =====================================================================
        // 액션 처리 — 선택 변경 시 BindToPlayer, 이후 Action1/Action2 호출
        // =====================================================================

        void HandleAction()
        {
            if (worldManager == null) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (Time.time - _lastActionTime < actionCooldown) return;

            var item = _inventory.SelectedItem;
            if (item == null) { _lastBoundItem = null; return; }

            // 선택 아이템이 바뀌었을 때만 재바인딩 (람다 재생성 최소화)
            if (item != _lastBoundItem)
            {
                item.data.BindToPlayer(item, this);
                _lastBoundItem = item;
            }

            bool acted = item.Action1() | item.Action2();
            if (acted) _lastActionTime = Time.time;
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

        // =====================================================================
        // 디버그 UI
        // =====================================================================

        void OnGUI()
        {
            if (!showDebugUI) return;
            if (_inventory == null) return;

            int cx = Screen.width / 2, cy = Screen.height / 2;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(cx - 20, cy - 1, 40, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1, cy - 20, 2, 40), Texture2D.whiteTexture);

            var sel = _inventory.SelectedItem;
            if (sel != null)
            {
                GUI.color = Color.yellow;
                GUI.Label(new Rect(cx + 30, cy - 10, 250, 25), $"[{sel.data.category}] {sel.data.itemName}");
                GUI.Label(new Rect(cx + 30, cy + 10, 250, 25), $"수량: {sel.count}");
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(10, 10, 200, 20), "=== 핫바 ===");
            for (int i = 0; i < _inventory.hotbarSize; i++)
            {
                var item   = _inventory.Hotbar[i];
                bool isSel = i == _inventory.SelectedSlotIndex;
                if (isSel)
                {
                    GUI.color = Color.yellow;
                    GUI.DrawTexture(new Rect(8, 32 + i * 24 - 2, 180, 22), Texture2D.whiteTexture);
                }
                GUI.color = isSel ? Color.black : Color.white;
                string label = item != null ? $"[{i+1}] {item.data.itemName} x{item.count}" : $"[{i+1}] -";
                GUI.Label(new Rect(12, 32 + i * 24, 160, 20), label);
                GUI.color = Color.white;
            }

            GUI.color = Color.white;
            int y = Screen.height - 120;
            GUI.Label(new Rect(10, y,      300, 20), "WASD: 이동 | Space: 점프 | ESC: 커서");
            GUI.Label(new Rect(10, y + 20, 300, 20), "좌클릭: 파기/설치  우클릭: 설치/사용");
            GUI.Label(new Rect(10, y + 40, 300, 20), "1~5: 핫바 선택 | 휠: 변경");
            GUI.Label(new Rect(10, y + 60, 300, 20), "I: 인벤토리");
        }
    }
}
