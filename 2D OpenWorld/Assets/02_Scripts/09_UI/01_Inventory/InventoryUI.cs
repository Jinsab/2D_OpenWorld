using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.15 오후 21:06
 *  마지막 수정 일자 : 26.04.11 오후 20:50
 *  
 *  [스크립트 목적 및 내용]
 *  1. 인벤토리 시스템 - 인벤토리 UI 관리
 *    1-1. UI에서 slots 리스트를 직접 수정하면 안됨(반드시 Inventory 함수를 통해 처리)
 *  
 *  2. 추후 고려 사항
 *    3-1. 슬롯이 비어있을 때 단순히 아이콘을 끄는 게 아니라, 약간 투명한 배경 칸이나 빈 슬롯 전용 가이드 이미지를 보여주기도 합니다.
 *    3-2. 슬롯 UI에 아이템의 내구도 바(Bar)나 쿨타임 표시 기능을 추가하고 싶음
 *    
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class InventoryUI : MonoBehaviour
{
    [Header("# Inventory Data")]
    public Inventory inventory;    // 인벤토리 데이터
    public TMP_Text inventoryName; // 인벤토리 이름
    public int lineCount = 10;     // 1줄 아이템 칸수
    public bool isPlayer = false;  // 플레이어

    [Header("# Slot Prefab")]
    public GameObject slotPrefab;
    public GameObject dropZone;

    [Header("# Slot Parent")]
    public RectTransform playerInventory;
    public RectTransform slotTopPanel;
    public RectTransform slotParent;

    [Header("# Slot Data")]
    public List<InventorySlotUI> uiSlots = new List<InventorySlotUI>();
    private GridLayoutGroup layoutGroup;

    private void Awake()
    {
        layoutGroup = slotParent.GetComponent<GridLayoutGroup>();
    }

    private void OnEnable()
    {
        // 중요: 인벤토리의 이벤트에 내 함수(RefreshSlot)를 연결(구독)
        Log.UI("인벤토리 창 오픈");
        AllDataRefresh();
        inventory.OnSlotChanged += RefreshSlot;
    }

    private void OnDisable()
    {
        // 메모리 누수 방지를 위해 오브젝트가 꺼질 때 구독 해제
        Log.UI("인벤토리 창 클로즈");
        inventory.OnSlotChanged -= RefreshSlot;
        TooltipUI.Instance.HideTooltip();
    }

    private void Start()
    {
        CreateSlots();
    }

    private void CreateSlots()
    {
        // 한 줄이 n인 인벤토리가 있다면, 최대 슬롯 / n칸 만큼의 줄 수가 필요함
        int line = Mathf.CeilToInt((float)inventory.inventoryData.maxSlots / lineCount);
        int currentSlotCount = 0;

        // Width(가로)와 Height(세로) 계산 예시
        // 1. 슬롯 1개의 크기는 100x100이라고 가정하자.
        // 2. 왼쪽/오른쪽/하단에 Border 수치는 각 20임
        // 3. 상단 Border 및 각 슬롯의 Spacing은 10임
        // 4. Width의 값은 (1줄의 슬롯 개수 * 100) + ((1줄의 슬롯 개수 - 1) * 10 + 40이 됨
        // 5. Height의 값은 (최대 슬롯 / 1줄의 n칸) * 110 + 20이 됨

        slotParent.sizeDelta = new Vector2(
            (lineCount * layoutGroup.cellSize.x) + ((lineCount - 1) * layoutGroup.spacing.x) + layoutGroup.padding.left * 2, // Width
            (line * (layoutGroup.cellSize.y + layoutGroup.spacing.y)) + layoutGroup.padding.top * 2);              // Height

        slotTopPanel.sizeDelta = new Vector2(slotParent.sizeDelta.x, 60);

        if (isPlayer)
        {
            // 플레이어의 기본 인벤토리는 20칸 이상임
            // 또한, 첫 줄엔 토글 넘버를 부여해서 사용하고 싶음.

            // 첫 번째 칸만 다르게 진행, 이후에는 동일하게 진행
            for (int i = 0; i < lineCount; i++)
            {
                GameObject obj = Instantiate(slotPrefab, slotParent);
                InventorySlotUI uiSlot = obj.GetComponent<InventorySlotUI>();
                uiSlot.isToggle = true;
                uiSlot.toggleText.text = $"{(i + 1) % 10}";
                uiSlot.Initialize(this.inventory, i);
                uiSlots.Add(uiSlot);
            }

            currentSlotCount += lineCount;
        }

        for (int i = currentSlotCount; i < inventory.inventoryData.maxSlots; i++)
        {
            GameObject obj = Instantiate(slotPrefab, slotParent);
            InventorySlotUI uiSlot = obj.GetComponent<InventorySlotUI>();
            uiSlot.Initialize(this.inventory, i);
            uiSlots.Add(uiSlot);
        }

        playerInventory.sizeDelta = new Vector2(slotParent.sizeDelta.x, slotParent.sizeDelta.y + slotTopPanel.sizeDelta.y);

        AllDataRefresh();
    }

    [ContextMenu("# Inventory Refresh Test")]
    public void AllDataRefresh()
    {
        Log.UI($"UI Count: {uiSlots.Count}");

        for (int i = 0; i < uiSlots.Count; i++)
        {
            uiSlots[i].UpdateVisual(inventory.inventoryData.slots[i]);
        }

        Log.UI("Refresh!");
    }

    // 이벤트가 발생하면 실행될 함수
    private void RefreshSlot(int index)
    {
        // 1. 인덱스 유효성 검사 (음수 방지 및 범위 체크)
        if (index < 0 || index >= uiSlots.Count || index >= inventory.inventoryData.slots.Count)
        {
            Log.Error("UI", $"Invalid Slot Index: {index}");
            return;
        }

        // 2. 해당 인덱스의 데이터와 UI 연결
        var data = inventory.inventoryData.slots[index];
        uiSlots[index].scaleFlag = false;
        uiSlots[index].UpdateVisual(data);
        AudioManager.Instance.Play(SND.UI_Item_Drop);

        Log.UI($"{index}번 UI 슬롯 갱신 완료 (ID: {data.itemId}, Qty: {data.amount})");
    }

    // 슬롯 위에 마우스가 올라가 있는 동안 호출됨
    //public void OnSlotHoverUpdate(int hoveredIndex)
    //{
    //    if (!isInventoryOpen) return;

    //    for (int i = 0; i < 10; i++)
    //    {
    //        KeyCode key = (i == 9) ? KeyCode.Alpha0 : KeyCode.Alpha1 + i;
    //        if (Input.GetKeyDown(key))
    //        {
    //            // hoveredIndex에 있는 아이템과 인벤토리의 i번 슬롯(퀵슬롯) 아이템을 스왑
    //            playerInventory.SwapSlots(hoveredIndex, i);
    //            break;
    //        }
    //    }
    //}
}
