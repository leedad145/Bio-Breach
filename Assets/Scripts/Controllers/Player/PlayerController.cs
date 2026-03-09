// =============================================================================
// PlayerController.cs - 1인칭 플레이어 컨트롤러
// =============================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using BioBreach.Engine.Entity;
using BioBreach.Engine.Inventory;
using BioBreach.Core.Voxel;
using BioBreach.Systems;
using BioBreach.Engine.Item;

namespace BioBreach.Controller.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInventory))]
    [RequireComponent(typeof(PlacementPreview))]
    public class PlayerController : EntityMonoBehaviour, IPlayerContext
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

        [Header("설치 가능한 네트워크 프리팹 목록")]
        [Tooltip("PlaceableItem이 설치할 수 있는 프리팹들. 인덱스로 ServerRpc에 전달됨.")]
        public List<GameObject> spawnablePrefabs = new();

        [Header("드롭 아이템")]
        [Tooltip("DroppedItem NetworkBehaviour가 붙은 프리팹. NetworkManager.NetworkPrefabs에도 등록 필요.")]
        public GameObject droppedItemPrefab;

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

        // 스탯 기준값 (Inspector에서 설정된 원본 값 — 장비 보너스 계산 기준)
        private float _baseMoveSpeed;
        private float _baseJumpHeight;

        // 임시 버프 누적값
        private float _speedBuff;
        private float _jumpBuff;

        // 채굴 HUD 스타일 캐시
        private GUIStyle _minerHudStyle;

        // 매 프레임 캐시
        private bool       _cachedHitValid;
        private RaycastHit _cachedHit;
        private bool       _primaryDown, _primaryHeld, _secondaryDown, _secondaryHeld;

        public bool UIBlocked { get; set; } = false;

        // SendMessage 경유 호출 (Systems 어셈블리의 SkillTreeTrigger 등이 사용)
        void SetUIBlocked(bool blocked) => UIBlocked = blocked;

        // =====================================================================
        // 네트워크 동기화 변수 (Owner가 쓰고 나머지가 읽는다)
        // =====================================================================

        private NetworkVariable<Vector3>    _netPos = new(Vector3.zero,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<float>      _netYaw = new(0f,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<float>      _netPitch = new(0f,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

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

        public float[] ModifyTerrain(Vector3 pos, float radius, float strength, VoxelType type)
            => worldManager != null ? worldManager.ModifyTerrain(pos, radius, strength, type) : new float[VoxelDatabase.TypeCount];

        // 공개 스탯 프로퍼티 (InventoryUI에서 분해 표시용)
        public float BaseMoveSpeed  => _baseMoveSpeed;
        public float BaseJumpHeight => _baseJumpHeight;
        public float BuffSpeed      => _speedBuff;
        public float BuffJump       => _jumpBuff;
        public float SkillSpeedBonus => PlayerSkillData.Instance?.TotalSpeedBonus ?? 0f;
        public float SkillJumpBonus  => PlayerSkillData.Instance?.TotalJumpBonus  ?? 0f;
        public float SkillHpBonus    => PlayerSkillData.Instance?.TotalHpBonus    ?? 0f;

        public void AddMoveSpeed(float v, float duration = 0f)
        {
            _speedBuff += v;
            RecalculateStats();
            if (duration > 0f) StartCoroutine(RevertBuff(v, 0f, duration));
        }

        public void AddJumpHeight(float v, float duration = 0f)
        {
            _jumpBuff += v;
            RecalculateStats();
            if (duration > 0f) StartCoroutine(RevertBuff(0f, v, duration));
        }

        private IEnumerator RevertBuff(float speedAmount, float jumpAmount, float delay)
        {
            yield return new WaitForSeconds(delay);
            _speedBuff -= speedAmount;
            _jumpBuff  -= jumpAmount;
            RecalculateStats();
        }

        /// <summary>현재 선택된 핫바 아이템을 장착한다. EquippableItem.Action1 → IPlayerContext.EquipSelectedItem 경로로 호출됨.</summary>
        public void EquipSelectedItem()
        {
            var inst = _inventory?.SelectedItem;
            if (inst != null) _inventory.TryEquip(inst);
        }

        /// <summary>장비 슬롯 / 버프 / 스킬 변경 시 moveSpeed·jumpHeight·maxHpBonus를 재계산한다.</summary>
        private void RecalculateStats()
        {
            if (_inventory == null) return;
            _inventory.GetEquipBonuses(out float hpBonus, out float speedBonus, out float jumpBonus);

            float skillSpeed = SkillSpeedBonus;
            float skillJump  = SkillJumpBonus;
            float skillHp    = SkillHpBonus;

            moveSpeed  = _baseMoveSpeed  + speedBonus + _speedBuff + skillSpeed;
            jumpHeight = _baseJumpHeight + jumpBonus  + _jumpBuff  + skillJump;
            maxHpBonus = hpBonus + skillHp;

            // 현재 HP가 새 MaxHp를 초과할 경우 서버에서 강제 감소
            if (IsServer && CurrentHp > MaxHp)
                TakeDamage(CurrentHp - MaxHp);
        }

        public void SpawnObject(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            if (prefab == null) return;

            int idx = spawnablePrefabs.IndexOf(prefab);
            if (idx >= 0)
            {
                // 목록에 등록된 NetworkObject 프리팹 → 서버에 인덱스로 스폰 요청
                SpawnNetworkObjectServerRpc(idx, pos, rot);
            }
            else
            {
                // 목록에 없는 프리팹 → 로컬 Instantiate (비-네트워크 이펙트 등)
                Debug.LogWarning($"[PlayerController] '{prefab.name}'이 spawnablePrefabs 목록에 없습니다. 로컬 생성.");
                Object.Instantiate(prefab, pos, rot);
            }
        }

        // =====================================================================
        // 아이템 월드 드롭
        // =====================================================================

        /// <summary>인벤토리에서 꺼낸 아이템을 플레이어 앞에 드롭한다. (클라이언트 호출)</summary>
        public void DropItemToWorld(string itemId, int count)
        {
            if (droppedItemPrefab == null)
            {
                Debug.LogWarning("[PlayerController] droppedItemPrefab이 설정되지 않았습니다.");
                return;
            }
            Vector3 dropPos = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;
            SpawnDroppedItemServerRpc(itemId, count, dropPos);
        }

        [ServerRpc]
        private void SpawnDroppedItemServerRpc(string itemId, int count, Vector3 pos)
        {
            if (droppedItemPrefab == null) return;

            var go = Object.Instantiate(droppedItemPrefab, pos, Quaternion.identity);
            if (!go.TryGetComponent<NetworkObject>(out var netObj))
            {
                Debug.LogWarning("[PlayerController] droppedItemPrefab에 NetworkObject가 없습니다.");
                Object.Destroy(go);
                return;
            }
            netObj.Spawn(destroyWithScene: true);
            if (go.TryGetComponent<DroppedItem>(out var dropped))
                dropped.Init(itemId, count);
        }

        [ServerRpc]
        private void SpawnNetworkObjectServerRpc(int prefabIndex, Vector3 pos, Quaternion rot)
        {
            if (prefabIndex < 0 || prefabIndex >= spawnablePrefabs.Count) return;

            var go = Object.Instantiate(spawnablePrefabs[prefabIndex], pos, rot);
            if (go.TryGetComponent<NetworkObject>(out var netObj))
                netObj.Spawn(destroyWithScene: true);
            else
                Debug.LogWarning($"[PlayerController] spawnablePrefabs[{prefabIndex}]에 NetworkObject가 없습니다.");
        }

        // =====================================================================
        // 초기화
        // =====================================================================

        // =====================================================================
        // 사망 처리 — 즉시 부활 (기본 Despawn 방지)
        // =====================================================================

        protected override void HandleDeath()
        {
            if (!IsServer) return;
            // 플레이어는 Despawn하지 않고 즉시 부활 (추후 게임 오버/대기 로직으로 교체 가능)
            Heal(MaxHp);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn(); // EntityMonoBehaviour HP 초기화 (NetworkVariable 구독 포함)

            _controller = GetComponent<CharacterController>();
            _inventory  = GetComponent<PlayerInventory>();
            _preview    = GetComponent<PlacementPreview>();

            // 스탯 기준값 저장 (Inspector 설정값 → 장비 보너스의 기반)
            _baseMoveSpeed  = moveSpeed;
            _baseJumpHeight = jumpHeight;

            // 장비 변경 / 스킬 해제 시 스탯 재계산 구독 (Owner만)
            if (IsOwner)
            {
                _inventory.OnEquipmentChanged += RecalculateStats;
                BioBreach.Systems.PlayerSkillData.OnSkillChanged += RecalculateStats;
                RecalculateStats(); // 스킬 보너스 즉시 반영
            }

            _camera    = GetComponentInChildren<Camera>();
            _spotLight = GetComponentInChildren<Light>();

            // WorldManager 자동 탐색 (Inspector 미연결 시)
            if (worldManager == null)
                worldManager = FindAnyObjectByType<WorldManager>();

            // 서버: 모든 플레이어 위치 주변 청크 로드 (AI 이동 등에 필요)
            // 클라이언트: 자신(IsOwner)의 위치 주변만 로드
            if (IsServer || IsOwner)
                worldManager?.RegisterViewer(transform, 5);

            if (_camera == null)
            {
                var camObj = new GameObject("PlayerCamera");
                camObj.transform.SetParent(transform);
                camObj.transform.localPosition = new Vector3(0, 0.6f, 0);
                _camera = camObj.AddComponent<Camera>();
            }

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

            // 소유자가 아니면 카메라·스팟라이트 비활성화 (다른 플레이어 시점 방지)
            bool owner = IsOwner;
            _camera.enabled    = owner;
            _spotLight.enabled = owner;

            if (owner)
            {
                // 스폰 즉시 현재 위치를 NetworkVariable에 기록
                // → 비소유자가 (0,0,0)으로 끌려가는 것을 방지
                _netPos.Value   = transform.position;
                _netYaw.Value   = transform.eulerAngles.y;
                _netPitch.Value = 0f;

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible   = false;
            }
            else
            {
                // 비소유자는 CharacterController를 끄고 NetworkVariable로 위치를 받는다
                _controller.enabled = false;
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn(); // EntityMonoBehaviour NetworkVariable 구독 해제
            if (_inventory != null)
                _inventory.OnEquipmentChanged -= RecalculateStats;
            BioBreach.Systems.PlayerSkillData.OnSkillChanged -= RecalculateStats;
            worldManager?.UnregisterViewer(transform);
        }

        // =====================================================================
        // 업데이트
        // =====================================================================

        void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner)
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
                HandleAction();
                HandleCursorLock();
                UpdateSpotLight();

                // 이동 후 최신 위치·방향을 NetworkVariable에 기록
                _netPos.Value   = transform.position;
                _netYaw.Value   = transform.eulerAngles.y;
                _netPitch.Value = _pitch;
            }
            else
            {
                // 비소유자: NetworkVariable 값으로 위치·회전 보간
                // _netPos가 아직 (0,0,0)이면 Owner가 아직 초기값을 전송하지 않은 상태 → 스킵
                if (_netPos.Value.sqrMagnitude < 0.001f) return;

                transform.SetPositionAndRotation(
                    Vector3.Lerp(transform.position, _netPos.Value, Time.deltaTime * 15f),
                    Quaternion.Lerp(transform.rotation, Quaternion.Euler(0f, _netYaw.Value, 0f), Time.deltaTime * 15f));

                if (_camera != null)
                    _camera.transform.localRotation = Quaternion.Lerp(
                        _camera.transform.localRotation,
                        Quaternion.Euler(_netPitch.Value, 0f, 0f),
                        Time.deltaTime * 15f);
            }
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
        // 액션 처리 — 선택 변경 시 BindToPlayer, 이후 Action1/Action2 호출
        // =====================================================================

        void HandleAction()
        {
            // worldManager가 아직 없으면 재탐색 (클라이언트 타이밍 대응)
            if (worldManager == null)
                worldManager = FindAnyObjectByType<WorldManager>();

            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (Time.time - _lastActionTime < actionCooldown) return;

            var item = _inventory.SelectedItem;
            if (item == null) return;

            var r1 = item.Action1(this);
            var r2 = item.Action2(this);
            if (r1.Performed || r2.Performed) _lastActionTime = Time.time;
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

        void DrawMinerHUD(UniversalMiner miner)
        {
            if (_minerHudStyle == null)
            {
                _minerHudStyle = new GUIStyle(GUI.skin.label) { fontSize = 10 };
                _minerHudStyle.normal.textColor = Color.white;
            }

            string[] names = { "", "단백질", "철분", "칼슘", "유전자정수", "지방", "골수" };
            var acc   = miner.Accumulation;
            float rowH    = 15f;
            float panelW  = 165f;
            float panelH  = rowH + 4 * (rowH + 2) + 8f;
            float px      = Screen.width  - panelW - 12f;
            float py      = Screen.height - panelH - 120f;

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(px - 4, py - 4, panelW + 8, panelH + 8), Texture2D.whiteTexture);

            GUI.color = new Color(0.7f, 0.9f, 1.0f);
            GUI.Label(new Rect(px, py, panelW, rowH), "채굴 진행도", _minerHudStyle);

            for (int i = 1; i <= 4; i++)
            {
                float ry        = py + rowH + (i - 1) * (rowH + 2);
                float progress  = acc[i];
                float threshold = BioBreach.Core.Voxel.VoxelDatabase.GetDropThreshold((BioBreach.Core.Voxel.VoxelType)i);
                float pct       = threshold > 0f ? Mathf.Clamp01(progress / threshold) : 0f;
                float barX      = px + 58f;
                float barW      = panelW - 62f;

                GUI.color = new Color(0.18f, 0.18f, 0.18f, 0.8f);
                GUI.DrawTexture(new Rect(barX, ry + 2, barW, rowH - 4), Texture2D.whiteTexture);
                if (pct > 0f)
                {
                    GUI.color = new Color(0.3f, 0.8f, 0.4f, 0.9f);
                    GUI.DrawTexture(new Rect(barX, ry + 2, barW * pct, rowH - 4), Texture2D.whiteTexture);
                }

                GUI.color = Color.white;
                GUI.Label(new Rect(px, ry, 58f, rowH), names[i], _minerHudStyle);
                GUI.Label(new Rect(barX + barW - 48f, ry, 50f, rowH),
                          $"{(int)progress}/{(int)threshold}", _minerHudStyle);
            }
            GUI.color = Color.white;
        }

        void OnGUI()
        {
            if (!IsOwner) return;
            if (_inventory == null) return;

            int cx = Screen.width / 2, cy = Screen.height / 2;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(cx - 20, cy - 1, 40, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1, cy - 20, 2, 40), Texture2D.whiteTexture);

            // 채굴 HUD — UniversalMiner 들고 있을 때 항상 표시
            var minerData = _inventory.SelectedItem?.data as UniversalMiner;
            if (minerData != null) DrawMinerHUD(minerData);

            if (!showDebugUI) return;

            var sel = _inventory.SelectedItem;
            if (sel != null)
            {
                GUI.color = Color.yellow;
                GUI.Label(new Rect(cx + 30, cy - 10, 250, 25), $"[{sel.data.GetType().Name}] {sel.data.itemName}");
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
