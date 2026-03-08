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
 *  1. 캐릭터 생성 시스템 - 캐릭터 외형 데이터 관리
 *    1-1. 성별 구분 (남성, 여성)
 *    1-2. 체형&피부, 얼굴, 눈 모양, 헤어 스타일, 기본 상·하의 라이브러리 데이터
 *    
 *  2. 큰 그림
 *    - Character Create System (캐릭터 생성 시스템)
 *      └─ CharacterDataManager (캐릭터 데이터 매니저) 
 *         └─ CharacterCreationManager (캐릭터 생성 매니저)
 *            ├─ CharacterAppearanceData (캐릭터 외형 데이터)
 *            └─ EquipmentOption (성별 전용 아이템)
 *               
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

[System.Serializable]
public class CharacterAppearanceData
{
    public bool gender;                   // 성별 (true: male, false: female)
    //public SpriteLibraryAsset hairAsset;  // 헤어 스타일
    //public SpriteLibraryAsset eyesAsset;  // 눈 모양
    //public SpriteLibraryAsset headAsset;  // 머리 & 피부색
    //public SpriteLibraryAsset chestAsset; // 기본 상의
    //public SpriteLibraryAsset pantsAsset; // 기본 하의
    //public SpriteLibraryAsset bodyAsset;  // 체형 & 몸통 & 피부색

    // 에셋 직접 참조 대신 에셋의 이름을 저장
    public string hairAssetName;
    public string hairAssetPath = "Assets/04_Sprite/04_Library/01_Hair/01_Human/";
    
    public string eyesAssetName;
    public string eyesAssetPath = "Assets/04_Sprite/04_Library/02_Eyes/01_Human/";
    
    public string headAssetName;
    public string headAssetPath = "Assets/04_Sprite/04_Library/03_Head/01_Human/";
    
    public string chestAssetName;
    public string chestAssetPath = "Assets/04_Sprite/04_Library/04_Chest/01_Human/";
    
    public string pantsAssetName;
    public string pantsAssetPath = "Assets/04_Sprite/04_Library/05_Pants/01_Human/";
    
    public string bodyAssetName;
    public string bodyAssetPath = "Assets/04_Sprite/04_Library/06_Body/01_Human/";

    public string GetFullAssetPath(string assetName, string assetPath)
    {
        return $"{assetPath}{assetName}.spriteLib";
    }

    public string GetFullAssetPath(string assetName, string assetPath, bool gender)
    {
        return $"{assetPath}{(gender == true ? "01_Male/" : "02_Female/")}{assetName}.spriteLib";
    }
}