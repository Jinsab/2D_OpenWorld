using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.15 오후 15:51
 *  마지막 수정 일자 : 26.02.15 오후 15:51
 *  
 *  [스크립트 목적 및 내용]
 *  1. 아이템 스크립트
 *    1-1. 아이템에 대한 기본 정보
 *    
 *  2. 큰 그림
 *    - Item (ScriptableObject)
 *      ├─ ItemData (기본 정보)
 *      ├─ ItemDatabase (데이터베이스)
 *      ├─ (Type)Item (아이템 타입)
 *      │  ├─ ResourceItem (원자재)
 *      │  │  ├─ Resource (원자재)
 *      │  │  └─ Material (가공 재료)
 *      │  │
 *      │  ├─ ConsumableItem (소비 아이템)
 *      │  │  ├─ Food (음식)
 *      │  │  ├─ Potion (물약)
 *      │  │  └─ ETC (기타)
 *      │  │
 *      │  ├─ Equipment (장비 아이템)
 *      │  │  ├─ ToolItem (도구형 아이템)
 *      │  │  └─ 추가 예정
 *      │  │
 *      │  ├─ Placeable (설치 아이템)
 *      │  └─ Quest (퀘스트 아이템)
 *      │
 *      ├─ ItemDropSpawner
 *      ├─ ItemDrop
 *      ├─ DropTable
 *      └─ DropData
 *  
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

[CreateAssetMenu(menuName = "Item/Resource Item")]
public class ResourceItem : Item
{
    public enum ResourceType
    {
        Raw,        // 원자재 (나무, 돌, 광석)
        Material    // 가공 재료 (판자, 철괴)
    }

    [Header("# Resource Info")]
    public ResourceType resourceType;

    public override void Use(GameObject user)
    {
        // 자원은 기본적으로 직접 사용하지 않음
        Debug.Log($"{itemName}은(는) 직접 사용할 수 없습니다.");
    }
}
