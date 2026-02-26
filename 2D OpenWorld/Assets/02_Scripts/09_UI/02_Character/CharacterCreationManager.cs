using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.U2D.Animation;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.25 오후 23:11
 *  마지막 수정 일자 : 26.02.25 오후 23:18
 *  
 *  [스크립트 목적 및 내용]
 *  1. 캐릭터 생성 시스템 - 캐릭터 생성 관리
 *    1-1. 성별, 체형&피부, 눈 색, 헤어 스타일, 헤어 색, 상의, 하의 결정 기능
 *    
 *  2. 큰 그림
 *    - Character Create System (캐릭터 생성 시스템)
 *      └─ CharacterDataManager (캐릭터 데이터 매니저) 
 *         └─ CharacterCreationManager (캐릭터 생성 매니저)
 *            ├─ CharacterAppearanceData (캐릭터 외형 데이터)
 *            └─ EquipmentOption (성별 전용 아이템)
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

    [Header("Preview Player")]
    public SpriteLibrary bodyLib;  // 플레이어 체형&피부 SpriteLibrary
    public SpriteLibrary headLib;  // 플레이어 머리 모양 SpriteLibrary
    public SpriteLibrary eyesLib;  // 플레이어의 눈 모양 SpriteLibrary
    public SpriteLibrary hairLib;  // 플레이어 헤어스타일 SpriteLibrary
    public SpriteLibrary chestLib; // 플레이어 상의 SpriteLibrary
    public SpriteLibrary pantsLib; // 플레이어 하의 SpriteLibrary

    [Header("Appearance Options")]
    public SpriteLibraryAsset[] headOptions;   // 전체 체형&피부 리스트
    public List<EquipmentOption> bodyOptions;   // 전체 체형&피부 리스트
    public List<EquipmentOption> eyesOptions;   // 전체 눈 색 리스트
    public SpriteLibraryAsset[] hairOptions;    // 전체 헤어스타일 리스트

    [Header("Current Selection")]
    public bool isMale = true; // 현재 선택된 성별

    [Header("Equipment Assets")]
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
        ApplyCurrentChest(); // 바뀐 성별의 상의
        ApplyCurrentPants(); // 바뀐 성별의 하의
        ApplyCurrentEyes();  // 바뀐 성별의 눈
        ApplyCurrentBody();  // 바뀐 성별의 체형&피부

        Debug.Log($"성별 전환 : {(isMale ? "남성" : "여성")}");
    }

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

    // 성별 버튼 클릭 시 호출
    public void SelectGender()
    {
        isMale = !isMale;
        GenderPreset selected = isMale ? malePreset : femalePreset;

        // 모든 부위의 라이브러리를 프리셋으로 즉시 교체
        //bodyLib.spriteLibraryAsset = selected.defaultBody;
        //headLib.spriteLibraryAsset = selected.defaultHead;
        //hairLib.spriteLibraryAsset = selected.defaultHair;
        //chestLib.spriteLibraryAsset = selected.defaultChest;
        //pantsLib.spriteLibraryAsset = selected.defaultPants;

        // UI 리스트 필터링 업데이트 (이전 답변 로직과 연동)
        RefreshFilteredOptions();
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

        ApplyCurrentEyes();
    }

    private void ApplyCurrentEyes()
    {
        if (filteredEyes.Count > 0)
        {
            eyesLib.spriteLibraryAsset = filteredEyes[currentEyesIndex].asset;
        }
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
        
        ApplyCurrentChest();
    }

    private void ApplyCurrentChest()
    {
        if (filteredChests.Count > 0)
        {
            chestLib.spriteLibraryAsset = filteredChests[currentChestIndex].asset;
        }
    }

    // [UI 버튼: 다음 하의로 변경]
    public void NextPants(int value)
    {
        if (filteredPants.Count == 0)
            return;

        currentPantsIndex =
            ((currentPantsIndex + value) % filteredPants.Count + filteredPants.Count) % filteredPants.Count;

        ApplyCurrentPants();
    }

    private void ApplyCurrentPants()
    {
        if (filteredPants.Count > 0)
        {
            pantsLib.spriteLibraryAsset = filteredPants[currentPantsIndex].asset;
        }
    }
    #endregion

    #region Head&Body
    // [UI 버튼: 다음 피부색으로 변경]
    public void NextBody(int value)
    {
        if (filteredBodys.Count == 0)
            return;

        currentBodyIndex = ((currentBodyIndex + value) % filteredBodys.Count + filteredBodys.Count) % filteredBodys.Count;
        Debug.Log($"체형&피부 변경: {currentBodyIndex} / {filteredBodys.Count}");

        ApplyCurrentBody();
    }

    private void ApplyCurrentBody()
    {
        if (filteredBodys.Count > 0)
        {
            bodyLib.spriteLibraryAsset = filteredBodys[currentBodyIndex].asset;
            UpdatePreview(headLib, headOptions[currentBodyIndex]); // 체형&피부 변경 시 머리도 같이 변경
        }
    }
    #endregion

    // UI 버튼: 다음 헤어 스타일로 변경
    public void NextHair()
    {
        currentHairIndex = (currentHairIndex + 1) % hairOptions.Length;
        UpdatePreview(hairLib, hairOptions[currentHairIndex]);
    }

    // 공통 업데이트 로직
    private void UpdatePreview(SpriteLibrary lib, SpriteLibraryAsset asset)
    {
        if (lib != null && asset != null)
        {
            lib.spriteLibraryAsset = asset;
        }
    }

    // 최종 선택 데이터 반환 (게임 시작 시 호출)
    public CharacterAppearanceData GetFinalData()
    {
        return new CharacterAppearanceData
        {
            //bodyAsset = bodyOptions[currentBodyIndex],
            //hairAsset = hairOptions[currentHairIndex],
            // ... 나머지 데이터 채우기
        };
    }
}