using JeffGrawAssets.FlexibleUI;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.27 오후 18:53
 *  마지막 수정 일자 : 26.03.09 오후 15:15
 *  
 *  [스크립트 목적 및 내용]
 *  1. 캐릭터 슬롯 시스템 - 캐릭터 선택 및 생성 칸 이동
 *    1-1. 슬롯에 따라 캐릭터 선택 또는 생성 화면으로 이동
 *    1-2. 슬롯에 유무에 따라 정보를 기입할 수 있어야 함.
 *    1-3. 빈 슬롯이면 클릭하여 캐릭터 데이터 생성 창으로 이동되어야 함.
 *    
 *  2. 큰 그림
 *    - Character Create System (캐릭터 생성 시스템)
 *      ├─ CharacterDataManager (캐릭터 데이터 매니저) 
 *      │  └─ CharacterCreationManager (캐릭터 생성 매니저)
 *      │     └─ CharacterData (캐릭터 전체 데이터)
 *      │        ├─ CharacterAppearanceData(캐릭터 외형 데이터)
 *      │        └─ EquipmentOption (성별 전용 아이템)
 *      │
 *      └─ CharacterSelectManager (캐릭터 선택 매니저 - 전체 슬롯 관리)
 *         └─ CharacterSlot (캐릭터 슬롯 데이터 - 슬롯 업데이트 스크립트)
 *               
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class CharacterPortrait : MonoBehaviour
{
    [Header("# Character Preview")]
    public FlexibleImage previewHair;
    public FlexibleImage previewEyes;
    public FlexibleImage previewHead;
    public FlexibleImage previewChest;
    public FlexibleImage previewPants;
    public FlexibleImage previewBody;

    [Header("# Visual Manager ")]
    public PlayerVisualManager visualManager;

    public async void SetPortrait(CharacterData data)
    {
        Debug.Log("Setting portrait for character: " + data.name);
        await visualManager.InitializePlayer(data.appearanceData);

        previewHair.sprite = visualManager.hairLib.GetSprite("Equip_Hair", "Idle_Left");
        previewEyes.sprite = visualManager.eyesLib.GetSprite("Player_Base_Eyes", "Side_0");
        previewHead.sprite = visualManager.headLib.GetSprite("Player_Base_Head", "Idle_Side_0");
        previewChest.sprite = visualManager.chestLib.GetSprite("Equip_Chest", "Idle_Left");
        previewPants.sprite = visualManager.pantsLib.GetSprite("Equip_Pants", "Idle_Left");
        previewBody.sprite = visualManager.bodyLib.GetSprite("Player_Base_Body", "Idle_Side_0");

        previewHair.rectTransform.sizeDelta =
            new Vector2(
                previewHair.sprite.rect.width * 6,
                previewHair.sprite.rect.height * 6);
        previewEyes.rectTransform.sizeDelta =
            new Vector2(
                previewEyes.sprite.rect.width * 6,
                previewEyes.sprite.rect.height * 6);
        previewHead.rectTransform.sizeDelta =
            new Vector2(
                previewHead.sprite.rect.width * 6,
                previewHead.sprite.rect.height * 6);
        previewChest.rectTransform.sizeDelta =
            new Vector2(
                previewChest.sprite.rect.width * 6,
                previewChest.sprite.rect.height * 6);
        previewPants.rectTransform.sizeDelta =
            new Vector2(
                previewPants.sprite.rect.width * 6,
                previewPants.sprite.rect.height * 6);
        previewBody.rectTransform.sizeDelta =
            new Vector2(
                previewBody.sprite.rect.width * 6,
                previewBody.sprite.rect.height * 6);

        // 여성 캐릭터의 경우 추가적인 위치 조정
        if (data.appearanceData.gender == false)
        {
            Debug.Log("성별이 여성이므로 세부 위치 조정이 필요합니다.");

            previewChest.rectTransform.anchoredPosition =
                new Vector2(
                    -3f,
                    previewChest.rectTransform.anchoredPosition.y);

            previewPants.rectTransform.anchoredPosition =
                new Vector2(
                    -3f,
                    previewPants.rectTransform.anchoredPosition.y);

            previewBody.rectTransform.anchoredPosition =
                new Vector2(
                    -3f,
                    previewBody.rectTransform.anchoredPosition.y);
        }
    }
}
