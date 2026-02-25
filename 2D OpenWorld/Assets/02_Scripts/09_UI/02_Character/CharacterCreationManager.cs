using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.U2D.Animation; // Sprite Library 사용을 위해 필수

public class CharacterCreationManager : MonoBehaviour
{
    public GenderPreset malePreset;
    public GenderPreset femalePreset;

    [Header("Preview Player")]
    public SpriteLibrary bodyLib;
    public SpriteLibrary headLib;
    public SpriteLibrary hairLib;
    public SpriteLibrary clothLib;

    [Header("Appearance Options")]
    public SpriteLibraryAsset[] bodyOptions;
    public SpriteLibraryAsset[] hairOptions;
    public SpriteLibraryAsset[] clothOptions;

    [Header("Current Selection")]
    public bool isMale = true; // 현재 선택된 성별

    [Header("Equipment Assets")]
    public List<EquipmentOption> chestOptions; // 전체 상의 리스트
    private List<EquipmentOption> filteredChests; // 현재 성별에 맞는 상의 리스트
    private int currentChestIndex = 0;

    public SpriteLibrary chestLib; // 플레이어의 상의 SpriteLibrary

    private int currentBodyIndex = 0;
    private int currentHairIndex = 0;
    private int currentClothIndex = 0;

    void Start()
    {
        RefreshFilteredOptions();
    }

    // [성별 변경 버튼 클릭 시 호출]
    public void ToggleGender(bool male)
    {
        isMale = male;
        currentChestIndex = 0; // 성별 변경 시 인덱스 초기화
        RefreshFilteredOptions();
        ApplyCurrentChest(); // 바뀐 성별 리스트의 첫 번째 옷 적용
    }

    // [성별에 맞는 아이템만 필터링]
    private void RefreshFilteredOptions()
    {
        if (isMale)
        {
            filteredChests = chestOptions.Where(opt => !opt.isFemaleOnly).ToList();
        }
        else
        {
            filteredChests = chestOptions.Where(opt => !opt.isMaleOnly).ToList();
        }
    }

    // [UI 버튼: 다음 옷 보기]
    public void NextChest()
    {
        if (filteredChests.Count == 0) return;
        currentChestIndex = (currentChestIndex + 1) % filteredChests.Count;
        ApplyCurrentChest();
    }

    private void ApplyCurrentChest()
    {
        if (filteredChests.Count > 0)
        {
            chestLib.spriteLibraryAsset = filteredChests[currentChestIndex].asset;
        }
    }

    // UI 버튼: 다음 피부색으로 변경
    public void NextBody()
    {
        currentBodyIndex = (currentBodyIndex + 1) % bodyOptions.Length;
        UpdatePreview(bodyLib, bodyOptions[currentBodyIndex]);
    }

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
            bodyAsset = bodyOptions[currentBodyIndex],
            hairAsset = hairOptions[currentHairIndex],
            // ... 나머지 데이터 채우기
        };
    }
}