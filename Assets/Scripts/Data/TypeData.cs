using UnityEngine;

[CreateAssetMenu(
    fileName = "TypeData",
    menuName = "Game/Data/Type Data"
)]
public class TypeData : ScriptableObject
{
    [Header("Šî–{î•ñ")]
    [SerializeField]
    private string typeName;

    [SerializeField]
    private string abbreviation;

    public string TypeName => typeName;
    public string Abbreviation => abbreviation;
}