// =============================================================================
// ItemBase.cs - 아이템 공통 데이터 추상 베이스
// =============================================================================
using System;
using UnityEngine;

namespace BioBreach.Engine.Item
{
    public abstract class ItemBase : IItem
    {
        public string dataId;
        public string itemName    = "새 아이템";
        public string description = "";
        public Sprite icon;
        public int    gridWidth   = 1;
        public int    gridHeight  = 1;
        public int    maxStack    = 99;

        protected static bool TryParseEnum<T>(string value, out T result) where T : struct, Enum
            => Enum.TryParse<T>(value, ignoreCase: true, out result);

        public abstract ActionResult Action1(IPlayerContext ctx);
        public abstract ActionResult Action2(IPlayerContext ctx);
    }
}
