using UnityEngine;

[CreateAssetMenu(fileName = "PropDatabase", menuName = "Game/Prop Database")]
public class PropDatabase : ScriptableObject
{
    public Mesh[] meshes;
    public Material[] materials;
    public Vector3[] colliderSizes;
    public Vector3[] colliderCenter;
}