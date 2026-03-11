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


            // Awaitable에서 await을 여 러번 사용할 때 
            // 처음 await으로 완료된 awaitableInstance는 풀로 돌아가기 때문에
            // 이미 await한 awaitableInstance를 다시 await하려고 할 때
            // 해당 Awaitable 인스턴스가 사실 다른 곳에서 실행된 async 메서드의
            // 반화 값으로 사용 중일 수 있기 때문에 예상하지 못한 동작이 일어날 수 있음
            // 본래라면 두 번째 await 코드 실행이 완료되어야 하지만,
            // 그러지 못하고 다른 작업을 대기하게 될 수 있음
            // 최악의 경우 데드락 상태를 유발할 수 있음

            await AddressableAssetLoader.Instance.TryAssetLoad(
                new string[]
                {
                    loadHairPath,
                    loadEyesPath,
                    loadHeadPath,
                    loadChestPath,
                    loadPantsPath,
                    loadBodyPath
                },
                new SpriteLibrary[]
                {
                    hairLib,
                    eyesLib,
                    headLib,
                    chestLib,
                    pantsLib,
                    bodyLib
                });

            // JSON에서 불러온 string 이름들로 각각 로드 시작
            //await loader.LoadAndApplyAsset(loadHairPath, hairLib);
            //await loader.LoadAndApplyAsset(loadEyesPath, eyesLib);
            //await loader.LoadAndApplyAsset(loadHeadPath, headLib);
            //await loader.LoadAndApplyAsset(loadChestPath, chestLib);
            //await loader.LoadAndApplyAsset(loadPantsPath, pantsLib);
            //await loader.LoadAndApplyAsset(loadBodyPath, bodyLib);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"캐릭터 외형 로드 중 오류 발생: {ex.Message}");
        }
    }
}
