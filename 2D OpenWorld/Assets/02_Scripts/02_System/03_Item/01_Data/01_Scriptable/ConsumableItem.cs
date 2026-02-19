using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.14 오후 21:15
 *  마지막 수정 일자 : 26.02.20 오전 01:44
 *  
 *  [스크립트 목적 및 내용]
 *  1. 아이템 시스템 - 도구 아이템
 *    1-1. 도구 데미지
 *    1-2. 도구 사용
 *    
 *  2. 큰 그림
 *    - Item (ScriptableObject)
 *      ├─ ItemData (기본 정보)
 *      ├─ ItemDatabase (데이터베이스)
 *      ├─ (Type)Item (아이템 타입)
 *      │  ├─ ConsumableItem (소비 아이템)
 *      │  └─ ToolItem (도구 아이템)
 *      │
 *      ├─ ItemDropSpawner
 *      ├─ ItemDrop
 *      ├─ DropTable
 *      └─ DropData
 *  
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

//[CreateAssetMenu(menuName = "Items/Consumable Item")]
public abstract class ConsumableItem : Item
{
    public enum ConsumableType
    {
        Food,       // 음식 (과일, 고기)
        Potion,     // 포션 (회복 포션(HP/MP/상태이상), 버프 계열)
        Explosive,  // 폭발물 (폭탄)
        Seed,       // 씨앗 아이템 (작물, 나무)
        Summon,     // 소환 아이템 (적 소환)
        Key,        // 열쇠 아이템 (등급)
    }

    [Header("# Resource Info")]
    public ConsumableType consumableType;

    //public override void Use(GameObject user)
    //{
    //    //PlayerStats stats = user.GetComponent<PlayerStats>();

    //    //if (stats == null)
    //    //    return;

    //    //if (restoreHP > 0)
    //    //    stats.RestoreHP(restoreHP);

    //    //if (restoreStamina > 0)
    //    //    stats.RestoreStamina(restoreStamina);

    //    // 사용했으므로 소비
    //    Inventory inventory = user.GetComponent<Inventory>();
    //    inventory.RemoveItem(itemId, 1);
    //}

    public abstract void Use(GameObject user);
}
