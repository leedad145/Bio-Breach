using System;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace BioBreach.Engine.Utils.UnityEngine
{
    /// <summary>
    /// 변수 타입과 변수 명으로 GameObject 시그니처를 만들어서 자식으로 재귀 탐색하여 바인딩.
    /// </summary>
    public abstract class ComponentBinder : MonoBehaviour
    {
        StringBuilder _stringBuilder;
        protected virtual void Awake()
        {
            _stringBuilder = new StringBuilder(35); // 35 => 제일 긴 prefix 글자수 + 변수이름 넉넉하게 잡은 수
            ResolveFields();
        }
        public void ResolveFields()
        {
            Type type = GetType();
            FieldInfo[] fieldInfos = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            foreach(FieldInfo fieldInfo in fieldInfos)
            {
                var bindAttribute = fieldInfo.GetCustomAttribute<BindAttribute>();

                if(bindAttribute == null)
                    continue;
                
                ResolveField(fieldInfo);
            }
        }
        void ResolveField(FieldInfo fieldInfo)
        {
            Type fieldType = fieldInfo.FieldType;
            string signature = ConvertToGameObjectSignature(fieldType, fieldInfo.Name);
            GameObject found = FindGameObjectbyName(signature);
            
            Debug.Assert(found != null, $"Falied to bind {signature}.");
            if(fieldType == typeof(GameObject))
            {
                fieldInfo.SetValue(this, found);
            }
            else if (typeof(Component).IsAssignableFrom(fieldType)) // 형 변환 가능한가?
            {
                Component component = found.GetComponent(fieldType);
                Debug.Assert(component != null, $"Falied to bind {signature}. Component is missing."); // 컴포넌트를 실수로 안 붙인걸로 추정됨.

                fieldInfo.SetValue(this, component);
            }
        }
        string ConvertToGameObjectSignature(Type fieldType, string fieldName)
        {
            _stringBuilder.Clear();

            if(GameObjectSignaturePrefixTable.TryGetPrefix(fieldType, out string prefix) == false)
            {
                prefix = string.Empty;
            }
            int startIdx = fieldName[0] == '_' ? 1 : 0;
            _stringBuilder.Append(prefix);
            _stringBuilder.Append(char.ToUpper(fieldName[startIdx]));
            for(int i = startIdx + 1; i < fieldName.Length; i++)
            {
                _stringBuilder.Append(fieldName[i]);
            }
            return _stringBuilder.ToString();
        }
        GameObject FindGameObjectbyName(string name)
        {
            if(transform.name.Equals(name))
                return transform.gameObject;

            return FindGameObjectByNameRecursively(transform, name);
        }
        GameObject FindGameObjectByNameRecursively(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if(child.name == name)
                    return child.gameObject;
                GameObject found = FindGameObjectByNameRecursively(child, name);
                if(found != null)
                    return found;
            }
            return null;
        }
    }
}

