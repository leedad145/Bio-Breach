// =============================================================================
// PlayerController.cs - 플레이어 오케스트레이터
//
// 책임:
//   - NetworkBehaviour 라이프사이클 (OnNetworkSpawn/Despawn)
//   - IPlayerContext 구현 (아이템이 플레이어에 접근하는 통합 인터페이스)
//   - 스탯 계산 (moveSpeed, jumpHeight, maxHpBonus)
//   - 네트워크 위치·회전 동기화
//   - 서브 컴포넌트 초기화 및 Update 오케스트레이션
//   - NetworkObject 스폰 / 아이템 드롭 ServerRpc
//
// 세부 로직은 각 서브 컴포넌트로 위임:
//   PlayerMovement — 물리 이동, 카메라, 스팟라이트
//   PlayerAction   — 전투/채굴/아이템 디스패치
//   PlayerHUD      — 디버그 GUI
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
using BioBreach.Engine.Data;
namespace BioBreach.Controller.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInventory))]
    [RequireComponent(typeof(PlacementPreview))]
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerAction))]
    [RequireComponent(typeof(PlayerHUD))]
    public class PlayerController : EntityMonoBehaviour, IPlayerContext
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("이동 스탯")]
        public float moveSpeed        = 10f;
        public float jumpHeight       = 3f;

        [Header("파기 / 설치 거리")]
        public float interactDistance  = 20f;
        public float placeNormalOffset = 0.05f;

        [Header("참조")]
        public WorldManager worldManager;

        [Header("설치 가능한 네트워크 프리팹 목록")]
        [Tooltip("PlaceableItem이 설치할 수 있는 프리팹들. 인덱스로 ServerRpc에 전달됨.")]
        public List<GameObject> spawnablePrefabs = new();

        [Header("드롭 아이템")]
        [Tooltip("DroppedItem NetworkBehaviour가 붙은 프리팹. NetworkManager.NetworkPrefabs에도 등록 필요.")]
        public GameObject droppedItemPrefab;

        // =====================================================================
        // 스탯 내부 상태
        // =====================================================================

        private float _baseMoveSpeed;
        private float _baseJumpHeight;
        private float _speedBuff;
        private float _jumpBuff;

        // =====================================================================
        // 컴포넌트 참조
        // =====================================================================

        private CharacterController _controller;
        private PlayerInventory     _inventory;
        private PlacementPreview    _preview;
        private Camera              _camera;
        private Light               _spotLight;

        private PlayerMovement _movement;
        private PlayerAction   _action;
        private PlayerHUD      _hud;

        // =====================================================================
        // 매 프레임 캐시 (IPlayerContext 에서 노출)
        // =====================================================================

        private bool       _cachedHitValid;
        private RaycastHit _cachedHit;
        private bool       _primaryDown, _primaryHeld, _secondaryDown, _secondaryHeld;

        public bool UIBlocked { get; set; } = false;

        // SendMessage 경유 호출 (Systems 어셈블리의 SkillTreeTrigger 등이 사용)
        void SetUIBlocked(bool blocked) => UIBlocked = blocked;

        // =====================================================================
        // 네트워크 동기화 변수
        // =====================================================================

        private NetworkVariable<Vector3> _netPos = new(Vector3.zero,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<float>   _netYaw = new(0f,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<float>   _netPitch = new(0f,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // =====================================================================
        // IPlayerContext 구현
        // =====================================================================

        public PlayerInventory Inventory        => _inventory;
        public float           PlaceNormalOffset => _action.PlaceNormalOffset;
        public bool            HasHit           => _cachedHitValid;
        public RaycastHit      Hit              => _cachedHit;
        public Vector3         AttackOrigin     => _camera != null ? _camera.transform.position : transform.position;
        public Vector3         AttackDirection  => _camera != null ? _camera.transform.forward  : transform.forward;
        public bool            PrimaryDown      => _primaryDown;
        public bool            PrimaryHeld      => _primaryHeld;
        public bool            SecondaryDown    => _secondaryDown;
        public bool            SecondaryHeld    => _secondaryHeld;

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

        // =====================================================================
        // 공개 스탯 프로퍼티 (InventoryUI 등 외부 참조용)
        // =====================================================================

        public float BaseMoveSpeed   => _baseMoveSpeed;
        public float BaseJumpHeight  => _baseJumpHeight;
        public float BuffSpeed       => _speedBuff;
        public float BuffJump        => _jumpBuff;
        public float SkillSpeedBonus => PlayerSkillData.Instance?.TotalSpeedBonus ?? 0f;
        public float SkillJumpBonus  => PlayerSkillData.Instance?.TotalJumpBonus  ?? 0f;
        public float SkillHpBonus    => PlayerSkillData.Instance?.TotalHpBonus    ?? 0f;

        // =====================================================================
        // IInventoryContext — 스탯 버프
        // =====================================================================

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

        public void EquipSelectedItem()
        {
            var inst = _inventory?.SelectedItem;
            if (inst != null) _inventory.TryEquip(inst);
        }

        private void RecalculateStats()
        {
            if (_inventory == null) return;
            _inventory.GetEquipBonuses(out float hpBonus, out float speedBonus, out float jumpBonus);

            moveSpeed  = _baseMoveSpeed  + speedBonus + _speedBuff + SkillSpeedBonus;
            jumpHeight = _baseJumpHeight + jumpBonus  + _jumpBuff  + SkillJumpBonus;
            maxHpBonus = hpBonus + SkillHpBonus;

            if (IsServer && CurrentHp > MaxHp)
                TakeDamage(CurrentHp - MaxHp);
        }

        // =====================================================================
        // IPlacementContext — 오브젝트 스폰
        // =====================================================================

        public void SpawnObject(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            if (prefab == null) return;
            int idx = spawnablePrefabs.IndexOf(prefab);
            if (idx >= 0)
                SpawnNetworkObjectServerRpc(idx, pos, rot);
            else
            {
                Debug.LogWarning($"[PlayerController] '{prefab.name}'이 spawnablePrefabs 목록에 없습니다. 로컬 생성.");
                Object.Instantiate(prefab, pos, rot);
            }
        }

        // =====================================================================
        // 아이템 월드 드롭
        // =====================================================================

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
        // 사망 처리
        // =====================================================================

        protected override void HandleDeath()
        {
            if (!IsServer) return;
            Heal(MaxHp); // 즉시 부활 (추후 게임 오버 로직으로 교체 가능)
        }

        // =====================================================================
        // 초기화
        // =====================================================================

        protected override void Start()
        {
            base.Start();
            GameDataLoader.EnsureLoaded();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _controller = GetComponent<CharacterController>();
            _inventory  = GetComponent<PlayerInventory>();
            _preview    = GetComponent<PlacementPreview>();
            _movement   = GetComponent<PlayerMovement>();
            _action     = GetComponent<PlayerAction>();
            _hud        = GetComponent<PlayerHUD>();

            _baseMoveSpeed  = moveSpeed;
            _baseJumpHeight = jumpHeight;

            if (IsOwner)
            {
                _inventory.OnEquipmentChanged += RecalculateStats;
                PlayerSkillData.OnSkillChanged += RecalculateStats;
                RecalculateStats();
            }

            _camera    = GetComponentInChildren<Camera>();
            _spotLight = GetComponentInChildren<Light>();

            if (worldManager == null)
                worldManager = FindAnyObjectByType<WorldManager>();

            if (IsServer || IsOwner)
                worldManager?.RegisterViewer(transform, 5);

            // 카메라 / 라이트 자동 생성 (프리팹 미설정 대비)
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

            bool owner = IsOwner;
            _camera.enabled    = owner;
            _spotLight.enabled = owner;

            // 서브 컴포넌트 초기화
            _movement.Init(_controller, _camera, _spotLight, _inventory);
            _action.Init(_inventory, worldManager);
            _hud.Init(this, _inventory, _action);

            if (owner)
            {
                _netPos.Value   = transform.position;
                _netYaw.Value   = transform.eulerAngles.y;
                _netPitch.Value = 0f;

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible   = false;
            }
            else
            {
                _controller.enabled = false;
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (_inventory != null)
                _inventory.OnEquipmentChanged -= RecalculateStats;
            PlayerSkillData.OnSkillChanged -= RecalculateStats;
            worldManager?.UnregisterViewer(transform);
        }

        // =====================================================================
        // 업데이트 — 서브 컴포넌트 오케스트레이션
        // =====================================================================

        void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner)
            {
                if (UIBlocked) { _preview.Hide(); return; }

                // WorldManager 지연 연결 (클라이언트 타이밍 대응)
                if (worldManager == null)
                {
                    worldManager = FindAnyObjectByType<WorldManager>();
                    if (worldManager != null) _action.UpdateWorldManager(worldManager);
                }

                _movement.Tick(moveSpeed, jumpHeight);
                CacheRaycast();
                CacheInput();
                _action.Tick(this);

                _netPos.Value   = transform.position;
                _netYaw.Value   = transform.eulerAngles.y;
                _netPitch.Value = _movement.Pitch;
            }
            else
            {
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
        // 내부 캐시
        // =====================================================================

        void CacheRaycast()
        {
            _cachedHitValid = false;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            if (Physics.Raycast(ray, out _cachedHit, _action.interactDistance))
                _cachedHitValid = true;
        }

        void CacheInput()
        {
            _primaryDown   = Input.GetMouseButtonDown(0);
            _primaryHeld   = Input.GetMouseButton(0);
            _secondaryDown = Input.GetMouseButtonDown(1);
            _secondaryHeld = Input.GetMouseButton(1);
        }
    }
}
