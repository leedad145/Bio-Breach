// =============================================================================
// PlayerController.cs - 1인칭 플레이어 컨트롤러 (그리드 인벤토리 + 설치 미리보기)
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
    public class PlayerController : MonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("이동")]
        public float moveSpeed       = 10f;
        public float jumpHeight      = 3f;
        public float gravityMultiplier = 2f;

        [Header("카메라")]
        public float mouseSensitivity = 2f;
        public float minPitch = -80f;
        public float maxPitch =  80f;

        [Header("파기 (VoxelBlock)")]
        public float interactDistance = 20f;

        [Header("설치 (Placeable)")]
        [Tooltip("설치 시 표면 법선 방향 오프셋")]
        public float placeNormalOffset = 0.05f;

        [Header("쿨다운")]
        public float actionCooldown = 0.1f;

        [Header("Ground Check")]
        public LayerMask groundLayer = ~0;

        [Header("참조")]
        public WorldManager worldManager;

        [Header("스팟라이트")]
        [Tooltip("카메라 회전을 따라가는 속도 (낮을수록 느리게)")]
        public float lightFollowSpeed = 6f;
        public float lightSpotAngle = 60f;
        public float lightRange = 30f;
        [Tooltip("빛 세기")]
        public float lightIntensity = 1f;

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

        // 이번 프레임 Raycast 캐시
        private bool       _cachedHitValid;
        private RaycastHit _cachedHit;

        // 현재 핫바 선택 아이템이 Placeable이고 미리보기 중인 prefab 추적
        private GameObject _lastPreviewPrefab;

        /// <summary>
        /// true이면 모든 플레이어 입력 차단 (인벤토리 UI 등에서 설정)
        /// </summary>
        public bool UIBlocked { get; set; } = false;

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
            _spotLight.spotAngle  = lightSpotAngle;
            _spotLight.range      = lightRange;
            _spotLight.intensity  = lightIntensity;
                
                

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        // =====================================================================
        // 업데이트
        // =====================================================================

        void Update()
        {
            // UI가 열려있으면 플레이어 입력 전부 차단
            if (UIBlocked)
            {
                _preview.Hide();
                return;
            }

            HandleMouseLook();
            HandleMovement();
            HandleSlotSelection();
            CacheRaycast();              // Raycast 1회
            UpdatePlacementPreview();    // 미리보기 (Placeable)
            HandleAction();              // 파기 / 설치 / 사용
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
        // 스팟라이트 (카메라보다 느리게 보간)
        // =====================================================================

        void UpdateSpotLight()
        {
            if (_spotLight == null || _camera == null) return;

            // _spotLight.spotAngle  = lightSpotAngle;
            // _spotLight.range      = lightRange;
            // _spotLight.intensity  = lightIntensity;
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

            // 수직 이동 먼저 적용 후 접지 체크
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

            float   radius      = _controller.radius * 0.9f;
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
        // Raycast 캐시 (매 프레임 1회)
        // =====================================================================

        void CacheRaycast()
        {
            _cachedHitValid = false;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            if (Physics.Raycast(ray, out _cachedHit, interactDistance))
                _cachedHitValid = true;
        }

        // =====================================================================
        // 설치 미리보기 업데이트
        // =====================================================================

        void UpdatePlacementPreview()
        {
            var selected = _inventory.SelectedItem;

            // Placeable 아이템이 아니면 미리보기 숨김
            if (selected == null || selected.data.category != ItemCategory.Placeable
                || selected.data.placeablePrefab == null)
            {
                _preview.Hide();
                _lastPreviewPrefab = null;
                return;
            }

            // 프리팹이 바뀌었으면 고스트 재생성
            var prefab = selected.data.previewPrefab != null
                ? selected.data.previewPrefab
                : selected.data.placeablePrefab;

            if (prefab != _lastPreviewPrefab)
            {
                _preview.Initialize(prefab);
                _lastPreviewPrefab = prefab;
            }

            if (!_cachedHitValid)
            {
                _preview.Hide();
                return;
            }

            Vector3    pos      = _cachedHit.point + _cachedHit.normal * placeNormalOffset;
            Quaternion rot      = Quaternion.FromToRotation(Vector3.up, _cachedHit.normal);
            bool       canPlace = CanPlaceAt(pos);

            _preview.UpdatePose(pos, rot, canPlace);
        }

        /// <summary>배치 위치가 플레이어와 겹치지 않는지 간단 체크</summary>
        bool CanPlaceAt(Vector3 pos)
        {
            return Vector3.Distance(pos, transform.position) > _controller.radius + 0.5f;
        }

        // =====================================================================
        // 액션 처리 (아이템에 위임)
        // =====================================================================

        void HandleAction()
        {
            if (worldManager == null) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (Time.time - _lastActionTime < actionCooldown) return;

            var selected = _inventory.SelectedItem;
            if (selected == null) return;

            var ctx = new ItemActionContext
            {
                Inventory         = _inventory,
                PlaceNormalOffset = placeNormalOffset,
                GetVoxelTypeAt    = GetVoxelTypeAtPoint,
                ModifyTerrain     = worldManager.ModifyTerrain,
                CanPlaceAt        = CanPlaceAt,
                AddMoveSpeed      = v => moveSpeed  += v,
                AddJumpHeight     = v => jumpHeight += v,
                AttackOrigin      = _camera.transform.position,
                AttackDirection   = _camera.transform.forward,
                HasHit            = _cachedHitValid,
                Hit               = _cachedHit,
                Item              = selected,
                PrimaryDown       = Input.GetMouseButtonDown(0),
                PrimaryHeld       = Input.GetMouseButton(0),
                SecondaryDown     = Input.GetMouseButtonDown(1),
                SecondaryHeld     = Input.GetMouseButton(1),
            };

            bool acted = selected.data.OnAction1(ctx) | selected.data.OnAction2(ctx);
            if (acted) _lastActionTime = Time.time;
        }

        // =====================================================================
        // 헬퍼
        // =====================================================================

        VoxelType GetVoxelTypeAtPoint(Vector3 worldPoint)
        {
            foreach (var kvp in worldManager.ActiveChunks)
            {
                var chunk = kvp.Value;
                if (chunk.ContainsPoint(worldPoint))
                    return chunk.GetVoxelTypeAt(worldPoint);
            }
            return VoxelType.Air;
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

            // 십자선
            int cx = Screen.width / 2, cy = Screen.height / 2;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(cx - 20, cy - 1, 40, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1, cy - 20, 2, 40), Texture2D.whiteTexture);

            // 선택 아이템 정보
            var sel = _inventory.SelectedItem;
            if (sel != null)
            {
                GUI.color = Color.yellow;
                GUI.Label(new Rect(cx + 30, cy - 10, 250, 25), $"[{sel.data.category}] {sel.data.itemName}");
                GUI.Label(new Rect(cx + 30, cy + 10, 250, 25), $"수량: {sel.count}");
            }

            // 핫바
            GUI.color = Color.white;
            GUI.Label(new Rect(10, 10, 200, 20), "=== 핫바 ===");
            for (int i = 0; i < _inventory.hotbarSize; i++)
            {
                var item = _inventory.Hotbar[i];
                bool isSel = i == _inventory.SelectedSlotIndex;
                if (isSel) { GUI.color = Color.yellow; GUI.DrawTexture(new Rect(8, 32 + i * 24 - 2, 180, 22), Texture2D.whiteTexture); }
                GUI.color = isSel ? Color.black : Color.white;
                string label = item != null ? $"[{i+1}] {item.data.itemName} x{item.count}" : $"[{i+1}] -";
                GUI.Label(new Rect(12, 32 + i * 24, 160, 20), label);
                GUI.color = Color.white;
            }

            // 조작법
            GUI.color = Color.white;
            int y = Screen.height - 120;
            GUI.Label(new Rect(10, y,      300, 20), "WASD: 이동 | Space: 점프 | ESC: 커서");
            GUI.Label(new Rect(10, y + 20, 300, 20), "좌클릭: 파기/설치  우클릭: 설치/사용");
            GUI.Label(new Rect(10, y + 40, 300, 20), "1~5: 핫바 선택 | 휠: 변경");
            GUI.Label(new Rect(10, y + 60, 300, 20), "I: 인벤토리 (UI 연결 필요)");
        }
    }
}
