using UnityEngine;
using UnityEngine.U2D.Animation; // Sprite Library 사용을 위해 필수

public class CharacterCreationManager : MonoBehaviour
{
    [Header("Preview Player")]
    public SpriteLibrary bodyLib;
    public SpriteLibrary headLib;
    public SpriteLibrary hairLib;
    public SpriteLibrary clothLib;

    [Header("Appearance Options")]
    public SpriteLibraryAsset[] bodyOptions;
    public SpriteLibraryAsset[] hairOptions;
    public SpriteLibraryAsset[] clothOptions;

    private int currentBodyIndex = 0;
    private int currentHairIndex = 0;
    private int currentClothIndex = 0;

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