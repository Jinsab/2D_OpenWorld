using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.14 오후 18:20
 *  마지막 수정 일자 : 26.03.23 오후 18:22
 *  
 *  [스크립트 목적 및 내용]
 *  1. 아이템 시스템 - 아이템 드롭
 *    1-1. 채집 이후 아이템 드롭
 *    1-2. 드롭 아이템 스프라이트 및 그림자 처리
 *    1-3. 일정 범위 진입 시 아이템 획득 로직 적용
 *    1-4. 로직을 거쳐 인벤토리 아이템 추가
 *    1-5. 아이템 추가 이후 아이템 삭제
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

public class ItemDrop : MonoBehaviour
{
    [Header("# Resource Value")]
    public Item item;      // 아이템
    public int amount = 1; // 아이템 수량
    public FloatingResource floating;
    [Tooltip("아이템이 끌려오는 속도")]
    public float pullSpeed = 0.5f;
    [Tooltip("아이템이 수집되는 거리")]
    public float pickupDistance = 0.5f;
    public float acceleration = 3f; // 끌려오는 속도의 가속도
    
    private SpriteRenderer itemSprite;
    private SpriteRenderer shadowSprite;
    private Collider2D coli;
    private Transform target; // 플레이어
    // private bool isPulling = false; // 아이템이 끌려오는 중인지 여부

    private void Awake()
    {
        coli = GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        itemSprite = GetComponent<SpriteRenderer>();
        shadowSprite = transform.GetChild(0).GetComponent<SpriteRenderer>();

        if (item != null)
        {
            itemSprite.sprite = item.Icon;
            shadowSprite.sprite = item.Icon;
        }
        else
        {
            Debug.Log("아이템 정보가 비어있습니다!");
        }
    }

    private void FixedUpdate()
    {
        if (!floating.floatingEnable || target == null || item == null)
            return;

        transform.position = Vector3.MoveTowards(
                transform.position,
                target.position,
                pullSpeed * Time.fixedDeltaTime);

        // 부드럽게 만들기 위해 가속도 주기
        pullSpeed += acceleration * Time.fixedDeltaTime;

        // 충분히 가까워지면 수집
        if (Vector3.Distance(transform.position, target.position) < pickupDistance)
        {
            CompletePickup();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            // 플레이어와 충돌 시 아이템 수집 처리
            OnTriggerEnter2D(collision.collider);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || floating.floatingEnable)
            return;

        if (other.TryGetComponent<Inventory>(out Inventory inv))
        {
            // 인벤토리에 추가할 수 없다면 수집하지 않음
            if (!(inv.GetAvailableSpace(item) >= amount))
                return;

            Log.Game("아이템 수집");
            target = other.transform;
            floating.floatingEnable = true;

            coli.enabled = false;
        }
    }

    public void Initialize(Item itemData, int amt)
    {
        item = itemData;
        amount = amt;

        gameObject.SetActive(true);
    }

    private void CompletePickup()
    {
        // 아이템 추가
        if (target.TryGetComponent<Inventory>(out Inventory inv))
        {
            // 인벤토리에 추가할 수 있으므로 수집함
            amount -= inv.AddItem(item, amount);

            Log.Game($"아이템 수집 완료: {item.itemName} {amount}개 남음");
            // 남은 아이템이 있는가?
            if (amount > 0)
            {
                // 아이템의 Amount를 깎고 나머지 수치를 정상화
                floating.floatingEnable = false;
                coli.enabled = true;
            }
            else
            {
                // 남은 아이템이 없으므로 삭제
                Destroy(gameObject);
            }
        }
    }
}