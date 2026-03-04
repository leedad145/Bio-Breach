// =============================================================================
// MarchingChunk.cs - 청크 하나의 메쉬를 생성하고 관리
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using BioBreach.Core.Voxel;
using BioBreach.Engine.Voxel;

namespace BioBreach.Engine.MarchingCubes
{
    /// <summary>
    /// 청크 하나의 Marching Cubes 메쉬를 생성하는 컴포넌트
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class MarchingChunk : MonoBehaviour
    {
        // =====================================================================
        // 청크 설정값
        // =====================================================================
        private int _sizeX, _sizeY, _sizeZ;
        private float _voxelSize;
        private Vector3 _chunkOffset;
        private Vector3 _seed;

        // =====================================================================
        // 복셀 데이터
        // =====================================================================
        private float[] _density;
        private VoxelType[] _voxelTypes;
        private bool _isInitialized = false;

        // =====================================================================
        // 메쉬 관련
        // =====================================================================
        private Mesh _mesh;
        private MeshCollider _meshCollider;
        private VoxelMaterialMap _materialMap;

        public void SetMaterialMap(VoxelMaterialMap map) { _materialMap = map; }

        // =====================================================================
        // 반구 설정
        // =====================================================================
        private float _hemisphereDiameter = 30f;
        private Vector3 _hemisphereCenter = Vector3.zero;

        // =====================================================================
        // 복셀 타입 확률 임계값 (0~1, 높을수록 희귀)
        // =====================================================================
        private float _ironThreshold     = 0.5f;
        private float _calciumThreshold  = 0.66f;
        private float _essenceThreshold  = 0.85f;

        // =====================================================================
        // 청크 생성
        // =====================================================================

        /// <summary>MapGenerator 등 외부에서 동적으로 설정을 주입할 때 사용 (CreateChunk 전에 호출)</summary>
        public void Configure(float hemisphereDiameter, Vector3 hemisphereCenter,
            float ironThreshold, float calciumThreshold, float essenceThreshold)
        {
            _hemisphereDiameter = hemisphereDiameter;
            _hemisphereCenter   = hemisphereCenter;
            _ironThreshold      = ironThreshold;
            _calciumThreshold   = calciumThreshold;
            _essenceThreshold   = essenceThreshold;
        }

        public void CreateChunk(int sizeX, int sizeY, int sizeZ, float voxelSize, Vector3 seed)
        {
            _sizeX = sizeX + 1;
            _sizeY = sizeY + 1;
            _sizeZ = sizeZ + 1;
            _voxelSize = voxelSize;
            _chunkOffset = transform.position;
            _seed = seed;

            int totalVoxels = _sizeX * _sizeY * _sizeZ;
            _density = new float[totalVoxels];
            _voxelTypes = new VoxelType[totalVoxels];
            _isInitialized = true;

            GenerateDensity();
            GenerateMesh();
        }

        // =====================================================================
        // 밀도 생성 - 반구 SDF
        // =====================================================================

        private void GenerateDensity()
        {
            float radius = _hemisphereDiameter * 0.5f;

            for (int x = 0; x < _sizeX; x++)
            for (int y = 0; y < _sizeY; y++)
            for (int z = 0; z < _sizeZ; z++)
            {
                int index = GetIndex(x, y, z);

                Vector3 worldPos  = new Vector3(x, y, z) * _voxelSize + _chunkOffset;
                Vector3 seededPos = worldPos + _seed;

                // 반구 SDF: 평면(y <= center.y) 아래는 항상 고체
                // 위 반구 내부(dist < radius) = 공기(양수), 외부 = 고체(음수)
                float density;
                if (worldPos.y <= _hemisphereCenter.y)
                    density = MIN_DENSITY;
                else
                    density = radius - Vector3.Distance(worldPos, _hemisphereCenter);

                _density[index]    = Mathf.Clamp(density, MIN_DENSITY, MAX_DENSITY);
                _voxelTypes[index] = DetermineVoxelType(seededPos);
            }
        }

        /// <summary>
        /// 생체 조직 복셀 타입 결정 — 깊이 구분 없이 균일 노이즈 분포.
        /// 임계값은 인스펙터에서 조작 가능 (높을수록 희귀).
        /// 우선순위: GeneticEssence > Calcium > Iron > Protein
        /// </summary>
        private VoxelType DetermineVoxelType(Vector3 seededPos)
        {
            float ironNoise = Perlin3D(
                seededPos.x * 0.08f + 100f,
                seededPos.y * 0.08f + 100f,
                seededPos.z * 0.08f + 100f
            );
            float calciumNoise = Perlin3D(
                seededPos.x * 0.06f + 200f,
                seededPos.y * 0.06f + 200f,
                seededPos.z * 0.06f + 200f
            );
            float essenceNoise = Perlin3D(
                seededPos.x * 0.1f  + 300f,
                seededPos.y * 0.1f  + 300f,
                seededPos.z * 0.1f  + 300f
            );

            if (essenceNoise > _essenceThreshold) return VoxelType.GeneticEssence;
            if (calciumNoise > _calciumThreshold) return VoxelType.Calcium;
            if (ironNoise    > _ironThreshold)    return VoxelType.Iron;
            return VoxelType.Protein;
        }

        // =====================================================================
        // 메쉬 생성 - Marching Cubes
        // =====================================================================
        
        private void GenerateMesh()
        {
            List<Vector3> vertices = new List<Vector3>();

            // VoxelType별 삼각형 인덱스 (Air=0 포함 5개, Air는 사용 안 함)
            List<int>[] trisByType = new List<int>[5];
            for (int i = 0; i < 5; i++) trisByType[i] = new List<int>();

            for (int x = 0; x < _sizeX - 1; x++)
            for (int y = 0; y < _sizeY - 1; y++)
            for (int z = 0; z < _sizeZ - 1; z++)
            {
                Polygonise(x, y, z, vertices, trisByType);
            }

            ApplyMesh(vertices, trisByType);
        }

        private void Polygonise(int x, int y, int z,
            List<Vector3> vertices, List<int>[] trisByType)
        {
            // 1. 큐브의 8개 꼭짓점 밀도
            float[] cube = new float[8];
            cube[0] = _density[GetIndex(x, y, z)];
            cube[1] = _density[GetIndex(x + 1, y, z)];
            cube[2] = _density[GetIndex(x + 1, y, z + 1)];
            cube[3] = _density[GetIndex(x, y, z + 1)];
            cube[4] = _density[GetIndex(x, y + 1, z)];
            cube[5] = _density[GetIndex(x + 1, y + 1, z)];
            cube[6] = _density[GetIndex(x + 1, y + 1, z + 1)];
            cube[7] = _density[GetIndex(x, y + 1, z + 1)];

            // 2. 큐브의 8개 꼭짓점 타입
            VoxelType[] types = new VoxelType[8];
            types[0] = _voxelTypes[GetIndex(x, y, z)];
            types[1] = _voxelTypes[GetIndex(x + 1, y, z)];
            types[2] = _voxelTypes[GetIndex(x + 1, y, z + 1)];
            types[3] = _voxelTypes[GetIndex(x, y, z + 1)];
            types[4] = _voxelTypes[GetIndex(x, y + 1, z)];
            types[5] = _voxelTypes[GetIndex(x + 1, y + 1, z)];
            types[6] = _voxelTypes[GetIndex(x + 1, y + 1, z + 1)];
            types[7] = _voxelTypes[GetIndex(x, y + 1, z + 1)];

            // 3. 큐브 인덱스 계산
            int cubeIndex = 0;
            float isoLevel = 0f;
            
            if (cube[0] < isoLevel) cubeIndex |= 1;
            if (cube[1] < isoLevel) cubeIndex |= 2;
            if (cube[2] < isoLevel) cubeIndex |= 4;
            if (cube[3] < isoLevel) cubeIndex |= 8;
            if (cube[4] < isoLevel) cubeIndex |= 16;
            if (cube[5] < isoLevel) cubeIndex |= 32;
            if (cube[6] < isoLevel) cubeIndex |= 64;
            if (cube[7] < isoLevel) cubeIndex |= 128;

            if (MarchingTables.edgeTable[cubeIndex] == 0) return;

            // 4. 큐브 꼭짓점 위치
            Vector3[] p = new Vector3[8];
            p[0] = new Vector3(x, y, z) * _voxelSize;
            p[1] = new Vector3(x + 1, y, z) * _voxelSize;
            p[2] = new Vector3(x + 1, y, z + 1) * _voxelSize;
            p[3] = new Vector3(x, y, z + 1) * _voxelSize;
            p[4] = new Vector3(x, y + 1, z) * _voxelSize;
            p[5] = new Vector3(x + 1, y + 1, z) * _voxelSize;
            p[6] = new Vector3(x + 1, y + 1, z + 1) * _voxelSize;
            p[7] = new Vector3(x, y + 1, z + 1) * _voxelSize;

            // 5. 엣지 보간
            Vector3[] vertList = new Vector3[12];

            int[,] edgeConnection = {
                {0,1}, {1,2}, {2,3}, {3,0},
                {4,5}, {5,6}, {6,7}, {7,4},
                {0,4}, {1,5}, {2,6}, {3,7}
            };

            int edge = MarchingTables.edgeTable[cubeIndex];

            for (int i = 0; i < 12; i++)
            {
                if ((edge & (1 << i)) != 0)
                {
                    int a = edgeConnection[i, 0];
                    int b = edgeConnection[i, 1];
                    vertList[i] = VertexInterp(p[a], p[b], cube[a], cube[b], isoLevel);
                }
            }

            // 6. 삼각형 생성 - 큐브의 고체 꼭짓점 다수결로 서브메시 결정
            VoxelType dominant = GetDominantType(cube, types, isoLevel);
            List<int> tris = trisByType[(int)dominant];

            for (int i = 0; MarchingTables.triTable[cubeIndex, i] != -1; i += 3)
            {
                int idx = vertices.Count;
                vertices.Add(vertList[MarchingTables.triTable[cubeIndex, i]]);
                vertices.Add(vertList[MarchingTables.triTable[cubeIndex, i + 1]]);
                vertices.Add(vertList[MarchingTables.triTable[cubeIndex, i + 2]]);
                tris.Add(idx);
                tris.Add(idx + 1);
                tris.Add(idx + 2);
            }
        }

        /// <summary>큐브의 고체 꼭짓점 중 가장 많은 VoxelType 반환</summary>
        private VoxelType GetDominantType(float[] cube, VoxelType[] types, float isoLevel)
        {
            int[] counts = new int[5];
            for (int i = 0; i < 8; i++)
                if (cube[i] < isoLevel)
                    counts[(int)types[i]]++;
            int best = 1; // Air(0) 제외, 기본 Protein
            for (int i = 2; i < 5; i++)
                if (counts[i] > counts[best]) best = i;
            return (VoxelType)best;
        }

        private Vector3 VertexInterp(Vector3 p1, Vector3 p2, float v1, float v2, float isoLevel)
        {
            if (Mathf.Abs(v1 - v2) < 0.00001f) return p1;
            float t = (isoLevel - v1) / (v2 - v1);
            return p1 + t * (p2 - p1);
        }

        private void ApplyMesh(List<Vector3> vertices, List<int>[] trisByType)
        {
            if (_mesh == null)
            {
                _mesh = new Mesh();
                _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            else
            {
                _mesh.Clear();
            }

            var meshRenderer = GetComponent<MeshRenderer>();

            if (vertices.Count == 0)
            {
                GetComponent<MeshFilter>().sharedMesh = _mesh;
                meshRenderer.sharedMaterials = new Material[0];
                return;
            }

            // Air(0) 제외, 삼각형이 있는 VoxelType만 서브메시로 등록
            var activeTypes = new List<int>();
            var matList     = new List<Material>();
            for (int i = 1; i < trisByType.Length; i++)
            {
                if (trisByType[i].Count > 0)
                {
                    activeTypes.Add(i);
                    matList.Add(_materialMap != null ? _materialMap.GetMaterial((VoxelType)i) : null);
                }
            }

            _mesh.SetVertices(vertices);
            _mesh.subMeshCount = activeTypes.Count;
            for (int s = 0; s < activeTypes.Count; s++)
                _mesh.SetTriangles(trisByType[activeTypes[s]], s);
            _mesh.RecalculateNormals();

            GetComponent<MeshFilter>().sharedMesh = _mesh;
            meshRenderer.sharedMaterials = matList.ToArray();

            if (_meshCollider == null)
            {
                _meshCollider = GetComponent<MeshCollider>();
                if (_meshCollider == null)
                    _meshCollider = gameObject.AddComponent<MeshCollider>();
            }
            _meshCollider.sharedMesh = null;
            _meshCollider.sharedMesh = _mesh;
        }

        // =====================================================================
        // 지형 수정 (파기/설치, 타입 지정 가능)
        // =====================================================================
        
        // 밀도 클램프 범위
        private const float MIN_DENSITY = -1f;  // 최대 고체
        private const float MAX_DENSITY = 1f;   // 최대 공기

        /// <summary>
        /// 밀도 수정 (파기/설치). 파기 시 실제로 제거된 고체 밀도 합계를 반환.
        /// 반환값은 항상 0 이상 — 설치나 변화 없을 땐 0.
        /// falloff: 중심에서 멀수록 약하게 적용.
        /// </summary>
        public float ModifyDensity(Vector3 worldPoint, float radius, float strength, VoxelType placeType = VoxelType.Air)
        {
            if (!_isInitialized) return 0f;

            Vector3 localPoint = worldPoint - _chunkOffset;
            bool  modified = false;
            float totalDug = 0f;

            int radiusVoxels = Mathf.CeilToInt(radius / _voxelSize) + 1;

            int centerX = Mathf.FloorToInt(localPoint.x / _voxelSize);
            int centerY = Mathf.FloorToInt(localPoint.y / _voxelSize);
            int centerZ = Mathf.FloorToInt(localPoint.z / _voxelSize);

            bool isPlacing = strength < 0;

            for (int x = centerX - radiusVoxels; x <= centerX + radiusVoxels; x++)
            for (int y = centerY - radiusVoxels; y <= centerY + radiusVoxels; y++)
            for (int z = centerZ - radiusVoxels; z <= centerZ + radiusVoxels; z++)
            {
                if (x < 0 || x >= _sizeX || y < 0 || y >= _sizeY || z < 0 || z >= _sizeZ)
                    continue;

                Vector3 voxelWorld = new Vector3(x, y, z) * _voxelSize + _chunkOffset;
                float dist = Vector3.Distance(voxelWorld, worldPoint);

                if (dist < radius)
                {
                    int index = GetIndex(x, y, z);

                    // 거리 감쇄 (중심이 강하고 외곽이 약함)
                    float falloff = 1f - (dist / radius);
                    falloff = falloff * falloff;

                    float before = _density[index];

                    // 파기일 때
                    if (!isPlacing)
                    {
                        float hardness = VoxelDatabase.GetHardness(_voxelTypes[index]);
                        if (hardness <= 0f) continue;

                        float adjustedStrength = strength / hardness;
                        _density[index] += adjustedStrength * falloff;
                    }
                    // 설치일 때
                    else
                    {
                        // 설치할 타입으로 변경
                        if (placeType != VoxelType.Air && _density[index] >= 0)
                        {
                            _voxelTypes[index] = placeType;
                        }

                        _density[index] += strength * falloff;
                    }

                    // 밀도 클램프 적용
                    _density[index] = Mathf.Clamp(_density[index], MIN_DENSITY, MAX_DENSITY);

                    // 파기: 고체 영역(density < 0)에서 실제 제거된 양 누적
                    if (!isPlacing && before < 0f)
                        totalDug += Mathf.Max(0f, Mathf.Min(_density[index], 0f) - before);

                    modified = true;
                }
            }

            if (modified) GenerateMesh();
            return totalDug;
        }

        /// <summary>
        /// 특정 위치의 복셀 타입 조회
        /// </summary>
        public VoxelType GetVoxelTypeAt(Vector3 worldPoint)
        {
            if (!_isInitialized) return VoxelType.Air;

            Vector3 localPoint = worldPoint - _chunkOffset;
            int x = Mathf.FloorToInt(localPoint.x / _voxelSize);
            int y = Mathf.FloorToInt(localPoint.y / _voxelSize);
            int z = Mathf.FloorToInt(localPoint.z / _voxelSize);

            if (x < 0 || x >= _sizeX || y < 0 || y >= _sizeY || z < 0 || z >= _sizeZ)
                return VoxelType.Air;

            return _voxelTypes[GetIndex(x, y, z)];
        }

        public bool ContainsPoint(Vector3 worldPoint, float margin = 0f)
        {
            Vector3 localPoint = worldPoint - _chunkOffset;
            float chunkSizeX = (_sizeX - 1) * _voxelSize;
            float chunkSizeY = (_sizeY - 1) * _voxelSize;
            float chunkSizeZ = (_sizeZ - 1) * _voxelSize;

            return localPoint.x >= -margin && localPoint.x <= chunkSizeX + margin &&
                   localPoint.y >= -margin && localPoint.y <= chunkSizeY + margin &&
                   localPoint.z >= -margin && localPoint.z <= chunkSizeZ + margin;
        }

        public void ClearDensityData()
        {
            _density = null;
            _voxelTypes = null;
            _isInitialized = false;
        }

        // =====================================================================
        // 유틸리티
        // =====================================================================
        
        private int GetIndex(int x, int y, int z)
        {
            return x + y * _sizeX + z * _sizeX * _sizeY;
        }


        private float Perlin3D(float x, float y, float z)
        {
            float xy = Mathf.PerlinNoise(x, y);
            float yz = Mathf.PerlinNoise(y, z);
            float xz = Mathf.PerlinNoise(x, z);
            return (xy + yz + xz) / 3f;
        }
    }
}
