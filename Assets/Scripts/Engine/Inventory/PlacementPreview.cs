// =============================================================================
// PlacementPreview.cs - 설치 전 반투명 미리보기
// =============================================================================
using UnityEngine;

namespace BioBreach.Engine.Inventory
{
    /// <summary>
    /// Placeable 아이템 설치 전 바닥에 갖다 댈 때 반투명 껍데기를 보여줌
    /// PlayerController에서 생성/업데이트/소멸 관리
    /// </summary>
    public class PlacementPreview : MonoBehaviour
    {
        // =====================================================================
        // 설정
        // =====================================================================
        
        [Header("미리보기 머티리얼")]
        [Tooltip("null이면 런타임에 자동 생성 (파란 반투명)")]
        public Material overrideMaterial;

        // =====================================================================
        // 내부
        // =====================================================================
        
        private GameObject _ghost;
        private Material   _previewMat;
        private bool       _isValid;

        // =====================================================================
        // 초기화 / 정리
        // =====================================================================

        /// <summary>
        /// 미리보기 초기화. 이미 생성돼 있으면 교체.
        /// </summary>
        public void Initialize(GameObject sourcePrefab)
        {
            Cleanup();

            _previewMat = overrideMaterial != null
                ? new Material(overrideMaterial)
                : CreateDefaultPreviewMat();

            _ghost = Instantiate(sourcePrefab);
            _ghost.name = "__PlacementGhost__";

            // 콜라이더/스크립트 제거 (순수 시각 전용)
            foreach (var col in _ghost.GetComponentsInChildren<Collider>())
                Destroy(col);
            foreach (var mono in _ghost.GetComponentsInChildren<MonoBehaviour>())
                Destroy(mono);

            // 모든 렌더러에 반투명 머티리얼 적용
            foreach (var rend in _ghost.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[rend.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = _previewMat;
                rend.materials = mats;
            }

            _ghost.SetActive(false);
        }

        public void Cleanup()
        {
            if (_ghost != null) Destroy(_ghost);
            if (_previewMat != null) Destroy(_previewMat);
            _ghost = null;
        }

        // =====================================================================
        // 매 프레임 업데이트 (PlayerController에서 호출)
        // =====================================================================

        /// <summary>
        /// 위치/색상 업데이트
        /// </summary>
        /// <param name="position">배치 위치</param>
        /// <param name="rotation">배치 회전</param>
        /// <param name="canPlace">배치 가능 여부 (색상 변경)</param>
        public void UpdatePose(Vector3 position, Quaternion rotation, bool canPlace)
        {
            if (_ghost == null) return;

            _ghost.SetActive(true);
            _ghost.transform.position = position;
            _ghost.transform.rotation = rotation;

            if (_isValid != canPlace)
            {
                _isValid = canPlace;
                UpdateColor();
            }
        }

        public void Hide()
        {
            if (_ghost != null) _ghost.SetActive(false);
        }

        // =====================================================================
        // 내부 헬퍼
        // =====================================================================

        private void UpdateColor()
        {
            if (_previewMat == null) return;
            // 배치 가능: 파란 반투명 / 불가: 빨간 반투명
            _previewMat.color = _isValid
                ? new Color(0.2f, 0.5f, 1f,  0.4f)
                : new Color(1f,   0.2f, 0.2f, 0.4f);
        }

        private static Material CreateDefaultPreviewMat()
        {
            // URP 환경이면 "Universal Render Pipeline/Lit" 으로 교체
            Shader shader = Shader.Find("Standard");
            var mat = new Material(shader)
            {
                color = new Color(0.2f, 0.5f, 1f, 0.4f)
            };

            // Standard 셰이더 투명 모드 설정
            mat.SetFloat("_Mode", 3);                        // Transparent
            mat.SetInt("_SrcBlend",  (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend",  (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite",    0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;

            return mat;
        }

        void OnDestroy() => Cleanup();
    }
}
