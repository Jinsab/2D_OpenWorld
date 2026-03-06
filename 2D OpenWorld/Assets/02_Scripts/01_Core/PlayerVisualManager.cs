using UnityEngine;
using UnityEngine.U2D.Animation;

public class PlayerVisualManager : MonoBehaviour
{
    public AddressableAssetLoader loader; // 위에서 만든 로더

    public SpriteLibrary hairLib;
    public SpriteLibrary eyesLib;
    public SpriteLibrary headLib;
    public SpriteLibrary chestLib;
    public SpriteLibrary pantsLib;
    public SpriteLibrary bodyLib;

    public void InitializePlayer(CharacterAppearanceData data)
    {
        // JSON에서 불러온 string 이름들로 각각 로드 시작
        loader.LoadAndApplyAsset(data.hairAssetName, hairLib);
        loader.LoadAndApplyAsset(data.eyesAssetName, eyesLib);
        loader.LoadAndApplyAsset(data.headAssetName, headLib);
        loader.LoadAndApplyAsset(data.chestAssetName, chestLib);
        loader.LoadAndApplyAsset(data.pantsAssetName, pantsLib);
        loader.LoadAndApplyAsset(data.bodyAssetName, bodyLib);
    }
}
