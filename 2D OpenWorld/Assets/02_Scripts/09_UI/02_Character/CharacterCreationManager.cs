using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.U2D.Animation;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.25 오후 23:11
 *  마지막 수정 일자 : 26.03.03 오후 14:48
 *  
 *  [스크립트 목적 및 내용]
 *  1. 캐릭터 생성 시스템 - 캐릭터 생성 관리
 *    1-1. 성별, 체형&피부, 눈 색, 헤어 스타일, 헤어 색, 상의, 하의 결정 기능
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
 *  1. https://github.com/dotnet/csharplang/issues/1408
 */

// 항상 양수 나머지를 얻는 방법 (수학적 Modulo)
// 음수에도 항상 양수의 나머지(예: -1 % 3 = 2)를 얻으려면
// 계산식에 피제수를 더하고 다시 나머지 연산을 수행해야 합니다.

public class CharacterCreationManager : MonoBehaviour
{
    public GenderPreset malePreset;
    public GenderPreset femalePreset;

    [Header("# Preview Player")]
    public SpriteLibrary bodyLib;  // 플레이어 체형&피부 SpriteLibrary
    public SpriteLibrary headLib;  // 플레이어 머리 모양 SpriteLibrary
    public SpriteLibrary eyesLib;  // 플레이어의 눈 모양 SpriteLibrary
    public SpriteLibrary hairLib;  // 플레이어 헤어스타일 SpriteLibrary
    public SpriteLibrary chestLib; // 플레이어 상의 SpriteLibrary
    public SpriteLibrary pantsLib; // 플레이어 하의 SpriteLibrary

    [Header("# Appearance Options")]
    public SpriteLibraryAsset[] headOptions;   // 전체 체형&피부 리스트
    public List<EquipmentOption> bodyOptions;   // 전체 체형&피부 리스트
    public List<EquipmentOption> eyesOptions;   // 전체 눈 색 리스트
    public SpriteLibraryAsset[] hairOptions;    // 전체 헤어스타일 리스트

    [Header("# Current Selection")]
    public bool isMale = true; // 현재 선택된 성별
    public TMP_InputField namaInput; 

    [Header("# Equipment Assets")]
    public List<EquipmentOption> chestOptions;    // 전체 상의 리스트
    public List<EquipmentOption> pantsOptions;    // 전체 하의 리스트
   
    private List<EquipmentOption> filteredChests; // 현재 성별에 맞는 상의 리스트
    private List<EquipmentOption> filteredPants;  // 현재 성별에 맞는 하의 리스트
    private List<EquipmentOption> filteredEyes;   // 현재 성별에 맞는 눈 모양 리스트
    private List<EquipmentOption> filteredBodys;  // 현재 성별에 맞는 체형&피부 리스트
    
    private int currentChestIndex = 0;
    private int currentPantsIndex = 0;
    private int currentEyesIndex = 0;
    private int currentBodyIndex = 0;
    private int currentHairIndex = 0;

    void Start()
    {
        RefreshFilteredOptions();
    }

    #region Gender
    // [성별 변경 버튼 클릭 시 호출]
    public void ToggleGender()
    {
        isMale = !isMale;
        //.. currentChestIndex = 0; // 성별 변경 시 인덱스 초기화

        RefreshFilteredOptions();

        // 바뀐 성별에 따라 외형(상의, 하의, 눈, 체형&피부) 정보 갱신
        ApplyCurrentData(filteredChests, chestLib, currentChestIndex); // 바뀐 성별의 상의
        ApplyCurrentData(filteredPants, pantsLib, currentPantsIndex);  // 바뀐 성별의 하의
        ApplyCurrentData(filteredEyes, eyesLib, currentEyesIndex);     // 바뀐 성별의 눈
        ApplyCurrentData(filteredBodys, bodyLib, currentBodyIndex);    // 바뀐 성별의 체형&피부

        // Debug.Log($"성별 전환 : {(isMale ? "남성" : "여성")}");
    }
    #endregion

    #region Eyes
    // [UI 버튼: 다음 눈으로 변경]
    public void NextEyes(int value)
    {
        if (filteredEyes.Count == 0)
            return;

        // 항상 양수 나머지를 얻는 방법
        currentEyesIndex =
            ((currentEyesIndex + value) % filteredEyes.Count + filteredEyes.Count) % filteredEyes.Count;

        ApplyCurrentData(filteredEyes, eyesLib, currentEyesIndex);
    }
    #endregion

    #region Cloth
    // [UI 버튼: 다음 상의로 변경]
    public void NextChest(int value)
    {
        if (filteredChests.Count == 0)
            return;

        currentChestIndex =
            ((currentChestIndex + value) % filteredChests.Count + filteredChests.Count) % filteredChests.Count;

        ApplyCurrentData(filteredChests, chestLib, currentChestIndex);
    }

    // [UI 버튼: 다음 하의로 변경]
    public void NextPants(int value)
    {
        if (filteredPants.Count == 0)
            return;

        currentPantsIndex =
            ((currentPantsIndex + value) % filteredPants.Count + filteredPants.Count) % filteredPants.Count;

        ApplyCurrentData(filteredPants, pantsLib, currentPantsIndex);
    }
    #endregion

    #region Head&Body
    // [UI 버튼: 다음 피부색으로 변경]
    public void NextBody(int value)
    {
        if (filteredBodys.Count == 0)
            return;

        currentBodyIndex = ((currentBodyIndex + value) % filteredBodys.Count + filteredBodys.Count) % filteredBodys.Count;
        UpdatePreview(headLib, headOptions[currentBodyIndex]); // 머리 모양도 체형&피부에 맞춰 변경
        Debug.Log($"체형&피부 변경: {currentBodyIndex} / {filteredBodys.Count}");

        ApplyCurrentData(filteredBodys, bodyLib, currentBodyIndex);
    }
    #endregion

    #region Hair
    // UI 버튼: 다음 헤어 스타일로 변경
    public void NextHair()
    {
        currentHairIndex = (currentHairIndex + 1) % hairOptions.Length;
        UpdatePreview(hairLib, hairOptions[currentHairIndex]);
    }
    #endregion

    #region Util
    // [성별에 맞는 아이템만 필터링]
    private void RefreshFilteredOptions()
    {
        if (isMale)
        {
            filteredChests = chestOptions.Where(opt => !opt.isFemaleOnly).ToList();
            filteredPants = pantsOptions.Where(opt => !opt.isFemaleOnly).ToList();
            filteredEyes = eyesOptions.Where(opt => !opt.isFemaleOnly).ToList();
            filteredBodys = bodyOptions.Where(opt => !opt.isFemaleOnly).ToList();
        }
        else
        {
            filteredChests = chestOptions.Where(opt => !opt.isMaleOnly).ToList();
            filteredPants = pantsOptions.Where(opt => !opt.isMaleOnly).ToList();
            filteredEyes = eyesOptions.Where(opt => !opt.isMaleOnly).ToList();
            filteredBodys = bodyOptions.Where(opt => !opt.isMaleOnly).ToList();
        }
    }

    // 공통 업데이트 로직
    private void ApplyCurrentData(List<EquipmentOption> filteredData, SpriteLibrary lib, int currentIndex)
    {
        if (filteredData.Count > 0)
        {
            lib.spriteLibraryAsset = filteredData[currentIndex].asset;
        }
    }

    private void UpdatePreview(SpriteLibrary lib, SpriteLibraryAsset asset)
    {
        if (lib != null && asset != null)
        {
            lib.spriteLibraryAsset = asset;
        }
    }
    #endregion

    public void OnClickStartGame()
    {
        // 1. 현재 선택된 에셋들을 데이터 매니저에 저장
        CharacterDataManager.Instance.playerData = GetFinalData();
        CharacterDataManager.Instance.SaveCharacterData();

        // 2. 인게임 씬으로 이동
        // UnityEngine.SceneManagement.SceneManager.LoadScene("02_InGameScene");
    }

    // 최종 선택 데이터 반환 (게임 시작 시 호출)
    public CharacterAppearanceData GetAppearanceData()
    {
        return new CharacterAppearanceData
        {
            gender = isMale,
            //bodyAsset = filteredBodys[currentBodyIndex].asset,
            //headAsset = headOptions[currentBodyIndex],
            //eyesAsset = filteredEyes[currentEyesIndex].asset,
            //hairAsset = hairOptions[currentHairIndex],
            //chestAsset = filteredChests[currentChestIndex].asset,
            //pantsAsset = filteredPants[currentPantsIndex].asset
            bodyAssetName = JsonUtility.ToJson(filteredBodys[currentBodyIndex].asset),
            headAssetName = JsonUtility.ToJson(headOptions[currentBodyIndex]),
            eyesAssetName = JsonUtility.ToJson(filteredEyes[currentEyesIndex].asset),
            hairAssetName = JsonUtility.ToJson(hairOptions[currentHairIndex]),
            chestAssetName = JsonUtility.ToJson(filteredChests[currentChestIndex].asset),
            pantsAssetName = JsonUtility.ToJson(filteredPants[currentPantsIndex].asset)
        };
    }

    public CharacterData GetFinalData()
    {
        return new CharacterData
        {
            name = namaInput.text, // 이름은 입력받는 UI 설정
            isEmpty = false, // 데이터 존재 여부 (캐릭터 슬롯이 비어있는지 여부)
            appearanceData = GetAppearanceData(), // 현재 선택된 외형 데이터
            statData = new PlayerStatData(), // 초기 스탯 데이터 (추후 커스터마이징 가능)
            inventoryData = new InventoryData(), // 초기 인벤토리
            level = 1, // 초기 레벨
            type = 0, // 초기 난이도 (예: 0 - 쉬움)
            playTime = 0f // 초기 플레이 시간
        };
    }
}