using UnityEngine.U2D.Animation;

[System.Serializable]
public class EquipmentOption
{
    public string itemName;
    public SpriteLibraryAsset asset;
    public bool isMaleOnly;   // 남성 전용 여부
    public bool isFemaleOnly; // 여성 전용 여부
    // 둘 다 false면 공용 아이템
}