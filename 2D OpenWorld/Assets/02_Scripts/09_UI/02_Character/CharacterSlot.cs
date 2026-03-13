using TMPro;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.27 오후 18:53
 *  마지막 수정 일자 : 26.03.13 오후 16:31
 *  
 *  [스크립트 목적 및 내용]
 *  1. 캐릭터 슬롯 시스템 - 캐릭터 선택 및 생성 칸 이동
 *    1-1. 슬롯에 따라 캐릭터 선택 또는 생성 화면으로 이동
 *    1-2. 슬롯에 유무에 따라 정보를 기입할 수 있어야 함.
 *    1-3. 빈 슬롯이면 클릭하여 캐릭터 데이터 생성 창으로 이동되어야 함.
 *    1-4. 캐릭터 삭제 시 해당 슬롯의 Index 정보에 따라 처리됨
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

    [Header("# Character Preview")]
    public CharacterPortrait portrait;

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
    public void PreviewCharacter()
    {
        // 일반적으로는 캐릭터 데이터를 넘겨주지만 예외 발생으로 인하여
        // 캐릭터 데이터가 null인 경우, 빈 슬롯으로 간주하여 UI를 업데이트 해야 함
        if (characterData != null)
        {
            if (!characterData.isEmpty)
            {
                infoGroup.SetActive(true);
                emptyGroup.SetActive(false);

                SetSlot(characterData);

                return;
            }
        }

        Log.UI("캐릭터 데이터가 null이거나 빈 슬롯입니다. 빈 슬롯으로 간주하여 UI 업데이트.");

        infoGroup.SetActive(false);
        emptyGroup.SetActive(true);
    }

    // 데이터를 받아서 슬롯 UI를 갱신하는 함수
    public void SetSlot(CharacterData data)
    {
        portrait.SetPortraitView(data);

        nameText.text = data.name;

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
        //if (data.statData.Stats.TryGetValue(StatType.MaxHealth, out CharacterStat HP))
        //    hpText.text = $"{HP.LastValue} HP";
        //else
        //    hpText.text = "HP Key Not Found";

        //if (data.statData.Stats.TryGetValue(StatType.MaxMana, out CharacterStat MP))
        //    mpText.text = $"{MP.LastValue} MP";
        //else
        //    mpText.text = "MP Key Not Found";

        if (data.statData != null)
        {
            CharacterStat HP = data.statData.Stats.Find(x => x.statType == StatType.MaxHealth).stat;
            CharacterStat MP = data.statData.Stats.Find(x => x.statType == StatType.MaxMana).stat;

            if (HP != null)
                hpText.text = $"{HP.Value} HP";
            else
                // hpText.text = "HP Key Not Found";
                hpText.text = "HP 100";

            if (MP != null)
                mpText.text = $"{MP.Value} MP";
            else
                // mpText.text = "MP Key Not Found";
                mpText.text = "MP 100";
        }

        levelText.text = $"{data.level} LV";
    }

    public void CharacterSelect(int index)
    {
        if (characterData.isEmpty || characterData == null)
        {
            // 캐릭터 정보가 없으므로 캐릭터 생성 화면으로 이동
            Log.UI("빈 슬롯 선택 - 캐릭터 생성 화면으로 이동");

            CharacterDataManager.Instance.characterIndex = index; // 선택한 슬롯 인덱스 저장

            slotManager.ShowCreateGroup();
        }
        else
        {
            // 캐릭터 정보가 있으므로 캐릭터 선택 완료, 해당 월드로 이동
            // 이후 절차적 맵 생성 시스템과 연동하여 맵 시드 정보도 전달해야 할 것임
            Log.UI($"캐릭터 선택 완료 - {characterData.name}, 월드로 이동");

            // 인벤토리 및 스탯은 순수 데이터이므로,
            // 플레이어가 접속하였을 때 실제로 사용되는 시점에
            // 데이터를 불러와서 적용하는 방식으로 진행해야 함

            // 그 이유는 MonoBehaviour에서 데이터를 직접 관리하게 될 경우,
            // 인벤토리 및 스탯 데이터를 Json 형태로 저장할 수 없었음
            // 그러므로, 인벤토리 및 스탯 데이터는 CharacterDataManager에서
            // 직접 관리하되 실제 데이터만 연결만 하는 것

            // 이것은 새로운 씬에서 Player가 생성되면
            // AddComponent<Inventory>()를 한 뒤,
            // 저장된 InventoryData를 주입(Injection)해 줍니다.

            // 2. 인게임 씬으로 이동
            UnityEngine.SceneManagement.SceneManager.LoadScene("02_InGameScene");
        }
    }

    public void SetDeleteSlotIndex(int index)
    {
        // 삭제 버튼은 캐릭터 정보가 있을 때에만 나타나므로, Empty 및 null 체크가 필요 없음
        slotManager.deleteSlotIndex = index;
    }
}
