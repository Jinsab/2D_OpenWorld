using UnityEngine.U2D.Animation;

[System.Serializable]
public class GenderPreset
{
    public string genderName;
    public SpriteLibraryAsset defaultBody;  // 몸통 (피부색)
    public SpriteLibraryAsset defaultHead;  // 머리
    public SpriteLibraryAsset defaultEyes;  // 눈 모양
    public SpriteLibraryAsset defaultHair;  // 머리카락
    public SpriteLibraryAsset defaultChest; // 상의
    public SpriteLibraryAsset defaultPants; // 하의
}