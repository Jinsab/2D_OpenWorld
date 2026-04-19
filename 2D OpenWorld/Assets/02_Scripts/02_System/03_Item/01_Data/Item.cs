using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.13 오후 20:46
 *  마지막 수정 일자 : 26.03.30 오전 03:39
 *  
 *  [스크립트 목적 및 내용]
 *  1. 아이템 스크립트
 *    1-1. 아이템에 대한 기본 정보
 *    
 *  2. 큰 그림
 *    - Item (ScriptableObject)
 *      ├─  ItemData        기본 정보
 *      ├─  ItemDatabase    데이터베이스
 *      ├─  (Type)Item      아이템 타입
 *      │  ├─ ResourceItem  재료
 *      │  │  ├─ Resource   원자재
 *      │  │  └─ Material   가공재
 *      │  │
 *      │  ├─ ConsumableItem (소비 아이템)
 *      │  │  ├─ Food (음식)
 *      │  │  ├─ Potion (물약)
 *      │  │  └─ ETC (기타)
 *      │  │
 *      │  ├─ Equipment (장비 아이템)
 *      │  │  ├─ WeaponItem (무기)
 *      │  │  │  ├─ Tool      도구 무기
 *      │  │  │  ├─ Melee     근접 무기
 *      │  │  │  ├─ Ranged    원거리 무기
 *      │  │  │  ├─ Magic     마법 무기
 *      │  │  │  ├─ Summon    소환 무기
 *      │  │  │  └─ Throwing  투척 무기
 *      │  │  │
 *      │  │  ├─ ArmorItem (방어구)
 *      │  │  │  ├─ Helmet  투구
 *      │  │  │  ├─ Tunic   튜닉
 *      │  │  │  ├─ Pants   바지
 *      │  │  │  └─ Cape    망토
 *      │  │  │
 *      │  │  └─ AccessoryItem (장신구)
 *      │  │     ├─ Ring    반지
 *      │  │     ├─ Pendant 펜던트
 *      │  │     ├─ Badge   배지
 *      │  │     ├─ Bag     가방
 *      │  │     └─ ETC     기타(가방, 이동 장비, 등불 등)
 *      │  │
 *      │  ├─ Placeable (설치 아이템)
 *      │  └─ Quest (퀘스트 아이템)
 *      │
 *      ├─ ItemDropSpawner
 *      ├─ ItemDrop
 *      ├─ ItemSet
 *      ├─ DropTable
 *      └─ DropData
 *  
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public abstract class Item : ScriptableObject
{
    public enum ItemType
    {
        Resource,     // 원자재
        Material,     // 가공 재료
        Equipment,    // 장착 아이템 (무기/도구/방어구)
        Consumable,   // 사용 즉시 효과
        Placeable,    // 설치 아이템
        Quest,        // 퀘스트 전용
    }

    public enum ItemRarity
    {
        Poor,       // 쓰레기
        Common,     // 일반
        Uncommon,   // 고급
        Rare,       // 희귀
        Epic,       // 서사
        Unique,     // 유일
        Legendary,  // 전설
        Mystic      // 신화
    }

    [Header("# Item Info")]
    public string itemName;             // 아이템 이름
    public int itemId;                  // 아이템 고유 아이디 (중복될 수 없음)
    [TextArea] public string itemDesc;  // 아이템 설명
    public Sprite Icon;                 // 아이템 아이콘

    public ItemType itemType;           // 아이템 타입
    public ItemRarity rarity;           // 아이템 희귀도 (0~5, 0 == 일반, 5 == 전설)
    public int itemLevel;               // 아이템 레벨 (0렙 = 미사용, 레벨이 높을수록 좋은 아이템)

    [Header("# Item Stack")]
    public int maxStack = 1;            // 최대 스택 갯수 (1 == 비스택)

    public bool isDroppable = true;     // 버리기 시스템
    public bool isDestroyable = true;   // 버리기 시스템

    public int buyPrice;                // 상점 시스템
    public int sellPrice;               // 상점 시스템

    [Header("# Item In Game View")]
    public Vector2 handOffset;          // 짧게 잡고 길게 잡는 등의 개별 설정 용도
    public float handRotation;          // 아이템마다 들고 있는 각도가 다를 수 있음
}
