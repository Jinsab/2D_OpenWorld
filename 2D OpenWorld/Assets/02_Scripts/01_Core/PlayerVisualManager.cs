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
        // Hair와 Head는 성별과 상관없이 로드하지만,
        // 나머지는 성별에 따라 다른 경로에서 로드할 수 있도록
        // GetFullAssetPath 메서드를 활용

        string loadHairPath = data.GetFullAssetPath(data.hairAssetName, data.hairAssetPath);
        string loadEyesPath = data.GetFullAssetPath(data.eyesAssetName, data.eyesAssetPath, data.gender);
        string loadHeadPath = data.GetFullAssetPath(data.headAssetName, data.headAssetPath);
        string loadChestPath = data.GetFullAssetPath(data.chestAssetName, data.chestAssetPath, data.gender);
        string loadPantsPath = data.GetFullAssetPath(data.pantsAssetName, data.pantsAssetPath, data.gender);
        string loadBodyPath = data.GetFullAssetPath(data.bodyAssetName, data.bodyAssetPath, data.gender);

        // JSON에서 불러온 string 이름들로 각각 로드 시작
        loader.LoadAndApplyAsset(loadHairPath, hairLib);
        loader.LoadAndApplyAsset(loadEyesPath, eyesLib);
        loader.LoadAndApplyAsset(loadHeadPath, headLib);
        loader.LoadAndApplyAsset(loadChestPath, chestLib);
        loader.LoadAndApplyAsset(loadPantsPath, pantsLib);
        loader.LoadAndApplyAsset(loadBodyPath, bodyLib);
    }
}
