using UnityEngine;

[CreateAssetMenu(fileName = "BasicScriptableObject", menuName = "ScriptableObjects/BasicScriptableObject")]
public class BasicScriptableObject : ScriptableObject
{
    public string m_StringData = "MyData";
    public int m_IntData = 23;
}
