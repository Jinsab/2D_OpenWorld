using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.04.15 오후 22:58
 *  마지막 수정 일자 : 26.04.24 오후 18:04
 *  
 *  [스크립트 목적 및 내용]
 *  1. 손에 든 아이템 시스템 - 플레이어가 손에 든 아이템을 관리하는 스크립트
 *    1-1. 퀵슬롯에서 선택된 아이템이 바뀔 때마다 손에 든 아이템을 갱신하는 역할
 *  
 *  2. 구현 중 고려해야 할 요소
 *    2-1. 아이템 위치 미세 조정 (Offset):
 *         - 칼은 손잡이를 잡아야 하고, 횃불은 아래쪽을 잡아야 합니다.
 *         - 각 아이템 프리팹 자체의 자식 오브젝트(Visual) 위치를 조절하여
 *           HandAnchor에 딱 맞게 세팅하세요.
 *    2-2. 레이어 순서 (Sorting Layer):
 *         - 아이템이 플레이어 몸 뒤로 가야 할 때와 앞으로 와야 할 때가 있습니다.
 *         - 플레이어의 바라보는 방향(상, 하, 좌, 우)에 따라
 *           SpriteRenderer.sortingOrder를 동적으로 조절하는 로직을
 *           PlayerHandController에 추가하면 좋습니다.
 *    2-3. 애니메이션 연동:
 *         - 플레이어가 걸을 때 손(HandAnchor)도 같이 위아래로 흔들리게
 *           애니메이션을 잡으면 아이템도 자연스럽게 같이 움직입니다
 *  
 *  3. 추후 고려 사항
 *    3-1. 현재는 단순히 이미지를 보여주는 기능
 *    3-2. 나중에 무기 휘두르기, 도구 사용, 소모품 먹기 등의 액션으로 연동 가능
 *    
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class PlayerHandController : MonoBehaviour
{
    [Header("# References")]
    [SerializeField] private QuickSlotManager quickSlotManager;
    [SerializeField] private Inventory playerInventory; // 인벤토리 직접 참조 추가
    [SerializeField] private Transform handAnchor; // 아이템이 붙을 위치
    [SerializeField] private PlayerLook playerLook; // 플레이어 방향 참조 용
    [SerializeField] private SpriteRenderer playerRenderer; // 플레이어 sortingOrder 참조 용
    [SerializeField] private EquipmentInventory equipmentInventory;

    [Header("# Current State")]
    [SerializeField] private SpriteRenderer handItemSprite; // 아이템 스프라이트
    // private Vector2 itemHandOffset; // 아이템 별 오프셋
    private int currentItemId = -1;
    private int currentSelectedIndex = 0;
    private int currentSortingOrder = 0;
    private Vector3 originalPos;
    private LookDirection lastLookDirection = LookDirection.Down;

    [SerializeField] private bool isActing = false;

    private void OnEnable()
    {
        // 퀵슬롯 선택 변경 이벤트 구독
        quickSlotManager.OnSlotSelected += HandleSlotSelected;
        playerInventory.OnSlotChanged += HandleItemChanged;
    }

    private void OnDisable()
    {
        // 퀵슬롯 선택 변경 이벤트 해제
        quickSlotManager.OnSlotSelected -= HandleSlotSelected;
        playerInventory.OnSlotChanged -= HandleItemChanged;
    }

    private void Update()
    {
        if (UIManager.Instance.CurrentState != UIManager.UIState.None)
            return;

        if (Mouse.current.leftButton.isPressed && !isActing)
        {
            StartCoroutine(PerformAction());
        }
    }
    private void LateUpdate()
    {
        HandSpritePosition();
        HandSpriteSortingOrder();
    }

    private void HandSpriteSortingOrder()
    {
        switch (lastLookDirection)
        {
            // 플레이어 앞에 표시해야 하는 경우이므로
            // player SortingOrder의 +6로 비교하면 됨 (파츠 5개의 앞에 표시 [5 + 1 = 6])
            case LookDirection.Left:
            case LookDirection.Right:
            case LookDirection.Down:
                if (currentSortingOrder + 6 != playerRenderer.sortingOrder)
                {
                    handItemSprite.sortingOrder = playerRenderer.sortingOrder + 6;
                }
                break;
            // 플레이어 뒤에 표시해야하는 경우이므로
            // player SortingOrder의 -1로 비교하면 됨 (파츠 뒤에 표시 [0 - 1 = -1])
            case LookDirection.Up:
                if (currentSortingOrder - 1 != playerRenderer.sortingOrder)
                {
                    handItemSprite.sortingOrder = playerRenderer.sortingOrder - 1;
                }
                break;
        }
    }

    private void HandSpritePosition()
    {
        if (lastLookDirection == playerLook.CurrentLookDirection)
            return;

        lastLookDirection = playerLook.CurrentLookDirection;
        // 추후에
        // 스프라이트 오프셋: Item 스크립트에 Vector2 handOffset 같은 변수를 추가해두면,
        // 칼은 손잡이 쪽이 손에 붙고, 방패는 중앙이 손에 붙도록 미세 조정이 가능

        // Left, Right, Down일 때에는 플레이어 앞에 있는 판정이고 (아이템이 플레이어 앞),
        // Up의 경우에는 플레이어 뒤에 있는 판정이 되어야 함 (플레이어가 아이템 앞)
        // 그러므로, sortingOrder를 적절히 조절해야 함
        
        switch (lastLookDirection)
        {
            case LookDirection.Left:
                handAnchor.transform.localPosition =
                    new Vector3(-0.25f, 0.35f, 0f);
                handAnchor.transform.localScale =
                    new Vector3(
                        Mathf.Abs(handAnchor.transform.localScale.x),
                        handAnchor.transform.localScale.y,
                        handAnchor.transform.localScale.z);
                break;

            case LookDirection.Right:
                handAnchor.transform.localPosition =
                    new Vector3(-0.25f, 0.35f, 0f);
                handAnchor.transform.localScale =
                    new Vector3(
                        Mathf.Abs(handAnchor.transform.localScale.x),
                        handAnchor.transform.localScale.y,
                        handAnchor.transform.localScale.z);
                break;

            case LookDirection.Up:
                handAnchor.transform.localPosition =
                    new Vector3(0.3f, 0.45f, 0f);
                handAnchor.transform.localScale =
                    new Vector3(
                        -Mathf.Abs(handAnchor.transform.localScale.x),
                        handAnchor.transform.localScale.y,
                        handAnchor.transform.localScale.z);
                break;

            case LookDirection.Down:
                handAnchor.transform.localPosition =
                    new Vector3(-0.3f, 0.35f, 0f);
                handAnchor.transform.localScale =
                    new Vector3(
                        Mathf.Abs(handAnchor.transform.localScale.x),
                        handAnchor.transform.localScale.y,
                        handAnchor.transform.localScale.z);
                break;
        }

        originalPos = handAnchor.localPosition;
    }

    private IEnumerator PerformAction()
    {
        isActing = true;

        Log.Game("Action 판정, 애니메이션 시작");
        Log.Game("현재 아이템 코드: " + currentItemId);
        if (ItemDatabase.Instance.TryGetItem(currentItemId, out Item data))
        {
            // 1. 시작 위치 설정 (원래 위치 + 아이템 고유의 시작 오프셋)
            Vector3 startPos = originalPos;
            switch (lastLookDirection)
            {
                case LookDirection.Left:
                case LookDirection.Right:
                    startPos = (Vector3)data.startSideOffset;
                    break;
                case LookDirection.Down:
                    startPos = (Vector3)data.startDownOffset;
                    break;
                case LookDirection.Up:
                    startPos = (Vector3)data.startUpOffset;
                    break;
            }
            handAnchor.localPosition = startPos;

            // 2. 마우스 방향 계산
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Mouse.current.position.value);
            Vector3 dir = (mousePos - transform.position).normalized;

            Log.Game("ActionType에 따른 분기 처리");
            // 3. ActionType에 따른 분기 처리
            switch (data.actionType)
            {
                case Item.UseActionType.Swing:
                    yield return StartCoroutine(SwingRoutine(startPos, dir, data));
                    break;
                case Item.UseActionType.Stab:
                    yield return StartCoroutine(StabRoutine(startPos, dir, data));
                    break;
                case Item.UseActionType.Consume:
                    yield return StartCoroutine(ConsumeRoutine(data));
                    break;
            }

            // 4. 복귀
            handAnchor.localPosition = originalPos;
            handAnchor.localRotation = Quaternion.Euler(0f, 0f, data.handRotation);
        }

        isActing = false;
    }

    private IEnumerator StabRoutine(Vector3 start, Vector3 dir, Item data)
    {
        Vector3 targetPos = start + (dir * data.actionRange);

        // 전진 (빠르게)
        float t = 0;
        while (t < 1)
        {
            t += Time.deltaTime / (data.actionDuration * 0.3f);
            handAnchor.localPosition = Vector3.Lerp(start, targetPos, t);
            yield return null;
        }
        // 후퇴 (느리게)
        t = 0;
        while (t < 1)
        {
            t += Time.deltaTime / (data.actionDuration * 0.7f);
            handAnchor.localPosition = Vector3.Lerp(targetPos, start, t);
            yield return null;
        }
    }

    private IEnumerator SwingRoutine(Vector3 start, Vector3 dir, Item data)
    {
        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        // float startAngle = baseAngle + 90f; // 각도 값 뒤에서 시작
        float endAngle = baseAngle - 90f;   // 각도 값 앞까지 휘두름

        float t = 0;
        
        while (t < 1)
        {
            t += Time.deltaTime / data.actionDuration;
            float currAngle = Mathf.Lerp(baseAngle, endAngle, t);
            handAnchor.localRotation = Quaternion.Euler(0, 0, currAngle);
            yield return null;
        }
    }

    private IEnumerator ConsumeRoutine(Item data)
    {
        yield return null;
    }

    // 슬롯 선택 번호가 바뀔 때 호출
    private void HandleSlotSelected(int index)
    {
        currentSelectedIndex = index;
        RefreshHand();
    }

    // 인벤토리의 특정 슬롯 내용이 바뀔 때 호출
    private void HandleItemChanged(int index)
    {
        // 핵심 로직: "방금 바뀐 슬롯이 내가 지금 손에 들고 있는 번호인가?"
        if (index == currentSelectedIndex)
        {
            RefreshHand();
        }
    }

    public void RefreshHand()
    {
        // 현재 선택된 슬롯의 아이템 데이터 가져오기
        currentItemId = playerInventory.inventoryData.slots[currentSelectedIndex].itemId;

        if (currentItemId <= 0)
        {
            handItemSprite.sprite = null;
            return;
        }

        Item itemData = ItemDatabase.Instance.GetItem(currentItemId);

        if (itemData != null)
        {
            // 프리팹 대신 아이템의 아이콘 스프라이트를 직접 적용
            handItemSprite.sprite = itemData.Icon;

            EquipmentItem equipmentItemData = itemData as EquipmentItem;

            if (equipmentItemData != null && equipmentItemData.slotType == EquipmentSlot.Weapon)
            {
                equipmentInventory.EquipWeapon(equipmentItemData);
            }
            else
            {
                equipmentInventory.UnEqiupWeapon();
            }

            // 필요 시 아이템 종류에 따라 크기나 각도 조절 로직 추가 가능
            // AdjustHandTransform(itemData);
        }
    }

    public void RefreshHand(int selectedIndex)
    {
        // 1. 선택된 슬롯의 아이템 정보 가져오기
        InventorySlot slot = quickSlotManager.GetSelectedSlot();

        // 2. 같은 아이템을 이미 들고 있다면 교체하지 않음 (최적화)
        if (currentItemId == slot.itemId) return;

        // 3. 기존에 들고 있던 오브젝트 파괴
        if (handItemSprite.sprite != null)
        {
            handItemSprite.sprite = null;
        }

        currentItemId = slot.itemId;

        // 4. 빈 슬롯이면 종료
        if (currentItemId <= 0) return;

        // 5. 아이템 데이터로부터 프리팹(외형) 생성
        Item itemData = ItemDatabase.Instance.GetItem(currentItemId);

        Log.Game("퀵슬롯 아이템: " + itemData.itemName);

        if (itemData != null && itemData.Icon != null)
        {
            handItemSprite.sprite = itemData.Icon;

            // 픽셀 아트 게임이라면 로컬 좌표 초기화가 중요합니다.
            handItemSprite.transform.localPosition = Vector3.zero;
            handItemSprite.transform.localRotation = Quaternion.identity;
        }
    }
}
