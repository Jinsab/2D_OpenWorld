using UnityEngine.U2D.Animation;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.25 오후 23:11
 *  마지막 수정 일자 : 26.02.25 오후 23:11
 *  
 *  [스크립트 목적 및 내용]
 *  1. 캐릭터 생성 시스템 - 성별 아이템 관리
 *    1-1. 아이템 이름
 *    1-2. 아이템 스프라이트 라이브러리
 *    1-3. 성별 구분 (남성 전용, 여성 전용, 공용 아이템)
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
public class EquipmentOption
{
    public string itemName;
    public SpriteLibraryAsset asset;
    public bool isMaleOnly;   // 남성 전용 여부
    public bool isFemaleOnly; // 여성 전용 여부
    // 둘 다 false면 공용 아이템
}