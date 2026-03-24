using TMPro;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.14 오후 18:20
 *  마지막 수정 일자 : 26.03.24 오후 18:20
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
    public TextMeshProUGUI amountText;
    [Tooltip("아이템이 끌려오는 속도")]
    public float pullSpeed = 0.5f;
    [Tooltip("아이템이 수집되는 거리")]
    public float pickupDistance = 0.5f;
    public float acceleration = 3f; // 끌려오는 속도의 가속도
    public bool canPickUp = false; // 아이템이 끌려오는 중인지 여부

    private SpriteRenderer itemSprite;
    private SpriteRenderer shadowSprite;
    private Collider2D coli;
    private Transform target; // 플레이어
    private float timer = 0f;

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

            if (amountText != null)
            {
                amountText.text = amount > 1 ? amount.ToString() : "";
                amountText.enabled = true;
            }
        }
        else
        {
            Log.Game("아이템 정보가 비어있습니다!");
        }
    }

    // 버린 아이템을 즉시 다시 줍는 것을 방지하기 위함
    // 수집 가능 상태 변수를 조정하는 역할을 함
    private void Update()
    {
        if (canPickUp || item == null)
            return;

        timer += Time.deltaTime;

        // 0.2초가 지나면 습득
        if (timer > 0.1f)
        {
            canPickUp = true;
        }
    }

    // OnTrigger를 통해서 아이템 수집이 가능하다고 이야기가 전환됨
    private void FixedUpdate()
    {
        if (floating.floatingEnable || target == null || item == null)
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

    // 일정 거리에 가까워지면 아이템 수집 범위에 들어왔다고 알림
    // 다만, 이 때 아이템 수집이 불가능한지를 확인해주는 로직만 사용
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || !canPickUp)
            return;

        if (other.TryGetComponent<Inventory>(out Inventory inv))
        {
            // 인벤토리에 추가할 수 없다면 수집하지 않음
            if (!(inv.GetAvailableSpace(item) >= amount))
                return;

            target = other.transform;
            floating.floatingEnable = false;
            coli.enabled = false;
        }
    }

    // 픽업 아이템의 경우 즉시 수집이 되어야 하고,
    // 플레이어가 버린 아이템의 경우에는 업데이트 문의 canPickUp 로직을 확인해야 함
    // isPickUp true = 즉시 수집 가능 (필드 드롭, 몬스터 드롭 아이템 등)
    // isPickUp false = 즉시 수집 가능 (플레이어 버린 아이템, 인벤토리 초과 아이템 등)
    public void Initialize(Item itemData, int amt, bool isPickUp)
    {
        item = itemData;
        amount = amt;
        canPickUp = isPickUp;

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
                floating.floatingEnable = true;
                canPickUp = false;
                timer = 0f;
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