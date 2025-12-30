using UnityEngine;


namespace MyNamespace
{
    public class Data
    {
        public string Info = "Some info";
        public bool Flag = true;
    }

    [CreateAssetMenu(fileName = "ScriptableObjectWithSerializeReference", menuName = "ScriptableObjects/ScriptableObjectWithSerializeReference")]
    public class ScriptableObjectWithSerializeReference : ScriptableObject
    {
        [SerializeReference]
        public System.Object reference = new Data();
    }
}

