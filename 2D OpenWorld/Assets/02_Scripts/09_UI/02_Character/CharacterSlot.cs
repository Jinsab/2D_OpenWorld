using TMPro;
using UnityEditor.U2D.Animation;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.27 오후 18:53
 *  마지막 수정 일자 : 26.03.02 오후 21:18
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

public class CharacterSlot : MonoBehaviour
{
    [Header("# Character Slot Manager")]
    public CharacterSlotManager slotManager;

    [Header("# Info Group")]
    public GameObject infoGroup;
    public CharacterData characterData;

    [Header("# Info Text")]
    public TMP_Text nameText;
    public TMP_Text typeText;
    public TMP_Text timeText;
    public TMP_Text hpText;
    public TMP_Text mpText;
    public TMP_Text levelText;
    
    [Header("# Empty Group")]
    public GameObject emptyGroup;

    // 슬롯이 활성화될 때마다 캐릭터 데이터에 따라 UI 업데이트
    private void OnEnable()
    {
        if (characterData.isEmpty)
        {
            infoGroup.SetActive(false);
            emptyGroup.SetActive(true);
        }
        else
        {
            infoGroup.SetActive(true);
            emptyGroup.SetActive(false);

            SetSlot(characterData);
        }
    }

    // 데이터를 받아서 슬롯 UI를 갱신하는 함수
    public void SetSlot(CharacterData data)
    {
        nameText.text = data.appearanceData.name;

        switch (data.type)
        {
            case 0:
                typeText.text = "쉬움";
                break;
            case 1:
                typeText.text = "보통";
                break;
            case 2:
                typeText.text = "어려움";
                break;
            default:
                typeText.text = "하드코어";
                break;
        }

        // 시간은 시:분:초로 변환하여 표시 (예시: 11h 30m 45s)
        timeText.text = $"{(int)(data.playTime / 3600)}h {(int)((data.playTime % 3600) / 60)}m {(int)(data.playTime % 60)}s";

        // HP와 MP는 StatType을 사용하여 캐릭터의 최대 체력과 최대 마나를 가져와서 표시
        // StatType.MaxHealth와 StatType.MaxMana는 enum으로 정의되어 있어야 하며, 캐릭터의 statData에서 해당 키로 값을 가져와야 합니다.
        // TryGetValue를 사용하여 해당 키가 존재하는지 확인하고, 존재하면 LastValue를 표시하고, 존재하지 않으면 "Key Not Found" 메시지를 표시합니다.
        if (data.statData.Stats.TryGetValue(StatType.MaxHealth, out CharacterStat HP))
            hpText.text = $"{HP.LastValue} HP";
        else
            hpText.text = "HP Key Not Found";

        if (data.statData.Stats.TryGetValue(StatType.MaxMana, out CharacterStat MP))
            mpText.text = $"{MP.LastValue} MP";
        else
            mpText.text = "MP Key Not Found";

        levelText.text = $"{data.level} LV";
    }

    public void CharacterSelect(int index)
    {
        if (characterData.isEmpty)
        {
            // 캐릭터 정보가 없으므로 캐릭터 생성 화면으로 이동
            Debug.Log("빈 슬롯 선택 - 캐릭터 생성 화면으로 이동");

            CharacterDataManager.Instance.characterIndex = index; // 선택한 슬롯 인덱스 저장

            slotManager.ShowCreateGroup();
        }
        else
        {
            // 캐릭터 정보가 있으므로 캐릭터 선택 완료, 해당 월드로 이동
            Debug.Log($"캐릭터 선택 완료 - {characterData.appearanceData.name}, 월드로 이동");
        
            
        }
    }
}
