using Arawn.CrystalSave.Runtime;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.25 오후 23:11
 *  마지막 수정 일자 : 26.03.06 오후 14:55
 *  
 *  [스크립트 목적 및 내용]
 *  1. 캐릭터 생성 시스템 - 캐릭터 데이터 관리
 *    1-1. 플레이어 최종 외형 정보 저장
 *    1-2. 이는 유지되어, 실제 인게임 씬으로 넘겨주어야 함
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

public class CharacterDataManager : MonoBehaviour
{
    public static CharacterDataManager Instance;

    // 임시: 캐릭터 슬롯 10칸 까지
    [Header("# Character List")]
    public CharacterData[] characterDataList;

    [Header("# Player Data")]
    public int characterIndex = 0;
    public CharacterData playerData;

    private bool IsInitialized = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            
            DontDestroyOnLoad(gameObject); // 씬 전환 시 파괴 방지
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        SaveManager.Initialized += (manager) =>
        {
            Debug.Log("SaveManager is ready!");

            IsInitialized = true;

            InitCharacterData(); // 캐릭터 데이터 리스트 초기화
        };
    }

    private void InitCharacterData()
    {
        // 저장된 데이터가 있다면 불러오고, 없다면 불러오지 않음
        Debug.Log("데이터 불러오기");

        characterDataList = new CharacterData[1];

        for (int i = 0; i < characterDataList.Length; i++)
        {
            // SaveManager.Instance.Load(i + 1, restoreLastActiveScene: false); // 저장된 데이터 로드
            
            // Debug.Log($"{i + 1}번 캐릭터 데이터 로드");
        }

        for (int i = 0; i < characterDataList.Length; i++)
        {
            characterDataList[i] = new CharacterData
            {
                isEmpty = true,
                appearanceData = new CharacterAppearanceData(),
                // 스탯 및 인벤토리 데이터는 캐릭터 생성 과정에서 연결만 하므로,
                // 여기서는 빈 데이터로 초기화
                // statData = new StatData(),
                inventoryData = new InventoryData(),
                level = 0,
                type = 0,
                playTime = 0f
            };
        }
    }

    public void SaveCharacterData()
    {
        if (IsInitialized)
        {
            Debug.Log($"{characterIndex}번 캐릭터 저장");

            // 현재 플레이어 데이터를 캐릭터 리스트에 저장
            characterDataList[characterIndex] = playerData;

            // 데이터 저장
            SaveManager.Instance.Save(characterIndex + 1);
        }
    }
}
