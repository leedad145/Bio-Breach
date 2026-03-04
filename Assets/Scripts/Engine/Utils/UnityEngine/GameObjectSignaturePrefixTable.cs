using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BioBreach.Engine.Utils.UnityEngine
{
    public static class GameObjectSignaturePrefixTable
    {
        public static bool TryGetPrefix(Type type, out string prefix)
        {
            if(s_prefixTable.TryGetValue(type, out prefix))
                return true;

            return false;
        }
        readonly static Dictionary<Type, string> s_prefixTable = new Dictionary<Type, string>()
        {
            { typeof(GameObject), ""},
            { typeof(Transform), ""},
            { typeof(RectTransform), ""},
            
            // 접두사
            { typeof(Toggle), "Toggle - "},
            { typeof(Button), "Button - "},
            { typeof(Image), "Image - "},
            { typeof(Canvas), "Canvas - "},
            { typeof(TMP_Text), "Text (TMP) - "},
            { typeof(TextMeshPro), "Text (TMP) - "},
            { typeof(TextMeshProUGUI), "Text (TMP) - "},
        };
    }
}