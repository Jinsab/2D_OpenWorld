using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.14 오후 18:20
 *  마지막 수정 일자 : 26.02.14 오후 21:26
 *  
 *  [스크립트 목적 및 내용]
 *  1. 아이템 시스템 - 아이템 드랍
 *    1-1. 인벤토리 아이템 추가
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
    [Tooltip("아이템이 끌려오는 속도")]
    public float pullSpeed = 0.5f;
    [Tooltip("아이템이 수집되는 거리")]
    public float pickupDistance = 0.5f;
    public float acceleration = 3f; // 끌려오는 속도의 가속도

    private new Rigidbody2D rigidbody;
    private Collider2D coli;
    private Transform target; // 플레이어
    private bool isPulling = false; // 아이템이 끌려오는 중인지 여부

    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody2D>();
        coli = GetComponent<Collider2D>();
    }

    private void FixedUpdate()
    {
        if (!isPulling || target == null)
            return;

        rigidbody.MovePosition(
            Vector3.MoveTowards(
                rigidbody.position,
                target.position,
                pullSpeed * Time.fixedDeltaTime)
        );

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
        if (collision.gameObject.CompareTag("Ground"))
        {
            rigidbody.simulated = true;
            coli.enabled = false;
        }
        else if (collision.gameObject.CompareTag("Player"))
        {
            // 플레이어와 충돌 시 아이템 수집 처리
            OnTriggerEnter2D(collision.collider);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || isPulling)
            return;

        if (other.TryGetComponent<Inventory>(out Inventory inv))
        {
            // 인벤토리에 추가할 수 없다면 수집하지 않음
            if (!inv.CanAdd(item, amount))
                return;

            target = other.transform;
            isPulling = true;

            coli.enabled = false;
        }
    }
    public void Initialize(Item itemData, int amt)
    {
        item = itemData;
        amount = amt;
    }

    private void CompletePickup()
    {
        // 아이템 추가
        if (target.TryGetComponent<Inventory>(out Inventory inv))
        {
            // 인벤토리에 추가할 수 있으므로 수집함
            amount -= inv.AddItem(item, amount);

            Debug.Log($"아이템 수집 완료: {item.itemName} {amount}개 남음");
            // 남은 아이템이 있는가?
            if (amount > 0)
            {
                // 아이템의 Amount를 깎고 나머지 수치를 정상화
                isPulling = false;
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