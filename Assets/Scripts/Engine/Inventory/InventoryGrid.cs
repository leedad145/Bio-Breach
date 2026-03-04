// =============================================================================
// InventoryGrid.cs - 디아블로2 스타일 그리드 인벤토리 데이터
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using BioBreach.Engine.Item;

namespace BioBreach.Engine.Inventory
{
    /// <summary>
    /// 그리드 상에 놓인 아이템 인스턴스
    /// </summary>
    public class ItemInstance
    {
        public ItemDataSO data;
        public int count;
        
        /// <summary>그리드 내 좌상단 위치</summary>
        public Vector2Int gridPos;
        
        /// <summary>현재 회전 여부 (가로/세로 전환)</summary>
        public bool isRotated;

        public int Width  => isRotated ? data.gridHeight : data.gridWidth;
        public int Height => isRotated ? data.gridWidth  : data.gridHeight;

        public ItemInstance(ItemDataSO data, int count, Vector2Int gridPos, bool isRotated = false)
        {
            this.data      = data;
            this.count     = count;
            this.gridPos   = gridPos;
            this.isRotated = isRotated;
        }
    }

    /// <summary>
    /// 디아블로2식 그리드 인벤토리 데이터 관리
    /// UI는 별도 InventoryUI.cs 에서 처리
    /// </summary>
    public class InventoryGrid
    {
        public readonly int Columns;
        public readonly int Rows;

        // 그리드 셀 → 아이템 인스턴스 참조 (빠른 조회용)
        private readonly ItemInstance[,] _grid;
        
        // 실제 아이템 목록
        private readonly List<ItemInstance> _items = new List<ItemInstance>();

        public IReadOnlyList<ItemInstance> Items => _items;

        public InventoryGrid(int columns, int rows)
        {
            Columns = columns;
            Rows    = rows;
            _grid   = new ItemInstance[columns, rows];
        }

        // =====================================================================
        // 배치 가능 여부
        // =====================================================================

        /// <summary>
        /// 해당 위치에 아이템을 배치할 수 있는지 확인
        /// </summary>
        public bool CanPlace(ItemDataSO data, Vector2Int pos, bool rotated = false)
        {
            int w = rotated ? data.gridHeight : data.gridWidth;
            int h = rotated ? data.gridWidth  : data.gridHeight;
            return CanPlace(w, h, pos);
        }

        public bool CanPlace(int w, int h, Vector2Int pos, ItemInstance ignore = null)
        {
            for (int x = pos.x; x < pos.x + w; x++)
            {
                for (int y = pos.y; y < pos.y + h; y++)
                {
                    if (x < 0 || x >= Columns || y < 0 || y >= Rows)
                        return false;
                    
                    ItemInstance occupant = _grid[x, y];
                    if (occupant != null && occupant != ignore)
                        return false;
                }
            }
            return true;
        }

        // =====================================================================
        // 추가 / 제거
        // =====================================================================

        /// <summary>
        /// 아이템을 특정 위치에 배치 (성공 여부 반환)
        /// </summary>
        public bool TryPlace(ItemDataSO data, int count, Vector2Int pos, bool rotated = false)
        {
            if (!CanPlace(data, pos, rotated)) return false;

            var instance = new ItemInstance(data, count, pos, rotated);
            _items.Add(instance);
            FillGrid(instance, instance.gridPos);
            return true;
        }

        /// <summary>
        /// 빈 공간을 자동 탐색해서 배치 (성공 여부 반환)
        /// </summary>
        public bool TryAddAuto(ItemDataSO data, int count = 1)
        {
            // 1. 스택 가능한 기존 슬롯 탐색
            if (data.maxStack > 1)
            {
                foreach (var item in _items)
                {
                    if (item.data == data && item.count < data.maxStack)
                    {
                        item.count = Mathf.Min(item.count + count, data.maxStack);
                        return true;
                    }
                }
            }

            // 2. 빈 공간 탐색 (회전 없이 먼저, 그 다음 회전)
            for (int rotPass = 0; rotPass < 2; rotPass++)
            {
                bool rotated = rotPass == 1;
                int w = rotated ? data.gridHeight : data.gridWidth;
                int h = rotated ? data.gridWidth  : data.gridHeight;

                for (int y = 0; y <= Rows - h; y++)
                {
                    for (int x = 0; x <= Columns - w; x++)
                    {
                        var pos = new Vector2Int(x, y);
                        if (CanPlace(w, h, pos))
                        {
                            var instance = new ItemInstance(data, count, pos, rotated);
                            _items.Add(instance);
                            FillGrid(instance, pos);
                            return true;
                        }
                    }
                }
            }

            return false; // 공간 없음
        }

        /// <summary>
        /// 아이템 인스턴스 제거
        /// </summary>
        public bool TryRemove(ItemInstance instance, int amount = 1)
        {
            if (!_items.Contains(instance)) return false;

            instance.count -= amount;
            if (instance.count <= 0)
            {
                ClearGrid(instance);
                _items.Remove(instance);
            }
            return true;
        }

        /// <summary>
        /// 아이템 이동 (드래그 앤 드롭용)
        /// </summary>
        public bool TryMove(ItemInstance instance, Vector2Int newPos, bool newRotated)
        {
            int w = newRotated ? instance.data.gridHeight : instance.data.gridWidth;
            int h = newRotated ? instance.data.gridWidth  : instance.data.gridHeight;

            if (!CanPlace(w, h, newPos, ignore: instance)) return false;

            ClearGrid(instance);
            instance.gridPos   = newPos;
            instance.isRotated = newRotated;
            FillGrid(instance, newPos);
            return true;
        }

        // =====================================================================
        // 조회
        // =====================================================================

        /// <summary>셀 좌표로 아이템 조회</summary>
        public ItemInstance GetAt(int x, int y)
        {
            if (x < 0 || x >= Columns || y < 0 || y >= Rows) return null;
            return _grid[x, y];
        }

        public ItemInstance GetAt(Vector2Int pos) => GetAt(pos.x, pos.y);

        /// <summary>특정 ItemData의 총 보유량</summary>
        public int CountOf(ItemDataSO data)
        {
            int total = 0;
            foreach (var item in _items)
                if (item.data == data) total += item.count;
            return total;
        }

        public bool Has(ItemDataSO data, int amount = 1) => CountOf(data) >= amount;

        // =====================================================================
        // 내부 헬퍼
        // =====================================================================

        private void FillGrid(ItemInstance instance, Vector2Int pos)
        {
            for (int x = pos.x; x < pos.x + instance.Width; x++)
                for (int y = pos.y; y < pos.y + instance.Height; y++)
                    _grid[x, y] = instance;
        }

        private void ClearGrid(ItemInstance instance)
        {
            for (int x = instance.gridPos.x; x < instance.gridPos.x + instance.Width; x++)
                for (int y = instance.gridPos.y; y < instance.gridPos.y + instance.Height; y++)
                    if (_grid[x, y] == instance)
                        _grid[x, y] = null;
        }
    }
}
