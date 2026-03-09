using UnityEngine;
using UnityEngine.U2D.Animation;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.09 오후 14:32
 *  마지막 수정 일자 : 26.03.09 오후 15:15
 *  
 *  [스크립트 목적 및 내용]
 *  1. 캐릭터 생성 시스템 - 캐릭터 데이터
 *    1-1. 유니티 Addressables 시스템을 활용
 *    1-2. JSON에서 불러온 string 이름으로 SpriteLibrary에 Sprite 적용
 *               
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class PlayerVisualManager : MonoBehaviour
{
    public AddressableAssetLoader loader; // 위에서 만든 로더

    public SpriteLibrary hairLib;
    public SpriteLibrary eyesLib;
    public SpriteLibrary headLib;
    public SpriteLibrary chestLib;
    public SpriteLibrary pantsLib;
    public SpriteLibrary bodyLib;

    public async Awaitable InitializePlayer(CharacterAppearanceData data)
    {
        try
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
            await loader.LoadAndApplyAsset(loadHairPath, hairLib);
            await loader.LoadAndApplyAsset(loadEyesPath, eyesLib);
            await loader.LoadAndApplyAsset(loadHeadPath, headLib);
            await loader.LoadAndApplyAsset(loadChestPath, chestLib);
            await loader.LoadAndApplyAsset(loadPantsPath, pantsLib);
            await loader.LoadAndApplyAsset(loadBodyPath, bodyLib);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"캐릭터 외형 로드 중 오류 발생: {ex.Message}");
        }
    }
}
