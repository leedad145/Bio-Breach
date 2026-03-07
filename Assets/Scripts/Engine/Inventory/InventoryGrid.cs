// =============================================================================
// InventoryGrid.cs - 디아블로2 스타일 그리드 인벤토리 데이터
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using BioBreach.Engine.Item;

namespace BioBreach.Engine.Inventory
{
    /// <summary>
    /// 그리드 상에 놓인 아이템 인스턴스.
    /// Action1/2(ctx) 호출 시 data(IItem)의 Action 실행 후
    /// ActionResult에 따라 인벤토리 Add/Remove를 처리한다.
    /// </summary>
    public class ItemInstance
    {
        public ItemBase data;
        public int        count;

        /// <summary>그리드 내 좌상단 위치</summary>
        public Vector2Int gridPos;

        /// <summary>현재 회전 여부 (가로/세로 전환)</summary>
        public bool isRotated;

        public int Width  => isRotated ? data.gridHeight : data.gridWidth;
        public int Height => isRotated ? data.gridWidth  : data.gridHeight;

        public ActionResult Action1(IPlayerContext ctx) => Dispatch(data.Action1(ctx), ctx);
        public ActionResult Action2(IPlayerContext ctx) => Dispatch(data.Action2(ctx), ctx);

        private ActionResult Dispatch(ActionResult r, IPlayerContext ctx)
        {
            if (!r.Performed) return r;
            if (r.AddItem != null && r.AddCount > 0)
                ctx.Inventory.TryAddItem(r.AddItem, r.AddCount);
            if (r.RemoveCount > 0)
                ctx.Inventory.TryRemoveItem(this, r.RemoveCount);
            return r;
        }

        public ItemInstance(ItemBase data, int count, Vector2Int gridPos, bool isRotated = false)
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

        public bool CanPlace(ItemBase data, Vector2Int pos, bool rotated = false)
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

        public bool TryPlace(ItemBase data, int count, Vector2Int pos, bool rotated = false)
        {
            if (!CanPlace(data, pos, rotated)) return false;

            var instance = new ItemInstance(data, count, pos, rotated);
            _items.Add(instance);
            FillGrid(instance, instance.gridPos);
            return true;
        }

        public bool TryAddAuto(ItemBase data, int count = 1)
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

            return false;
        }

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

        public ItemInstance GetAt(int x, int y)
        {
            if (x < 0 || x >= Columns || y < 0 || y >= Rows) return null;
            return _grid[x, y];
        }

        public ItemInstance GetAt(Vector2Int pos) => GetAt(pos.x, pos.y);

        public int CountOf(ItemBase data)
        {
            int total = 0;
            foreach (var item in _items)
                if (item.data == data) total += item.count;
            return total;
        }

        public bool Has(ItemBase data, int amount = 1) => CountOf(data) >= amount;

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

        // =====================================================================
        // 분할 / 합치기
        // =====================================================================

        /// <summary>
        /// 스택을 splitAmount만큼 분할해 새 ItemInstance를 그리드 빈 자리에 배치한다.
        /// 빈 자리가 없으면 null 반환 (원본 스택은 변경되지 않음).
        /// </summary>
        public ItemInstance TrySplit(ItemInstance source, int splitAmount)
        {
            if (source == null || source.count <= 1) return null;
            int take = Mathf.Clamp(splitAmount, 1, source.count - 1);

            int w = source.isRotated ? source.data.gridHeight : source.data.gridWidth;
            int h = source.isRotated ? source.data.gridWidth  : source.data.gridHeight;

            for (int rotPass = 0; rotPass < 2; rotPass++)
            {
                bool rotated = rotPass == 1;
                int rw = rotated ? source.data.gridHeight : source.data.gridWidth;
                int rh = rotated ? source.data.gridWidth  : source.data.gridHeight;

                for (int cy = 0; cy <= Rows - rh; cy++)
                {
                    for (int cx = 0; cx <= Columns - rw; cx++)
                    {
                        var pos = new Vector2Int(cx, cy);
                        // 원본 위치와 겹치면 스킵 (같은 아이템을 ignore로 처리)
                        if (!CanPlace(rw, rh, pos, ignore: source)) continue;
                        // 원본과 완전히 같은 위치·크기면 스킵 (이동이 아닌 분할이므로)
                        if (pos == source.gridPos && rotated == source.isRotated) continue;

                        source.count -= take;
                        var newInst = new ItemInstance(source.data, take, pos, rotated);
                        _items.Add(newInst);
                        FillGrid(newInst, pos);
                        return newInst;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 같은 dataId 아이템 두 스택을 합친다 (maxStack 상한 적용).
        /// from이 소진되면 그리드에서 제거한다.
        /// 반환값: 실제로 합쳐진 수량 (0이면 실패).
        /// </summary>
        public int TryMerge(ItemInstance from, ItemInstance to)
        {
            if (from == null || to == null || from == to) return 0;
            if (from.data.dataId != to.data.dataId) return 0;

            int canTake = to.data.maxStack - to.count;
            if (canTake <= 0) return 0;

            int take = Mathf.Min(canTake, from.count);
            to.count   += take;
            from.count -= take;

            if (from.count <= 0)
            {
                ClearGrid(from);
                _items.Remove(from);
            }
            return take;
        }
    }
}
