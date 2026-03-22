using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.15 오후 21:06
 *  마지막 수정 일자 : 26.03.17 오후 16:51
 *  
 *  [스크립트 목적 및 내용]
 *  1. 인벤토리 시스템 - 인벤토리 UI 관리
 *    1-1. UI에서 slots 리스트를 직접 수정하면 안됨(반드시 Inventory 함수를 통해 처리)
 *    
 *  2. 큰 그림
 *    - Inventory System
 *      ├─ Inventory (인벤토리 데이터 로직)
 *      └─ Inventory UI (전체 UI 관리)
 *         ├─ InventorySlotUI (슬롯 단위 UI)
 *         ├─ DragController (마우스 드래그 전담)
 *         ├─ DragIconUI (아이템 드래그 시 아이콘 표시)
 *         ├─ TooltipUI (아이템 설명 표시)
 *         └─ SlotUIInteraction (마우스 호버 시 툴팁 표시 및 하이라이트 효과)
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

    private List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();
    private GridLayoutGroup layoutGroup;

    private void Awake()
    {
        layoutGroup = slotParent.GetComponent<GridLayoutGroup>();
    }

    private void Start()
    {
        CreateSlots();
        Refresh();
    }

    void CreateSlots()
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
                InventorySlotUI slotUI = obj.GetComponent<InventorySlotUI>();
                slotUI.isToggle = true;
                slotUI.toggleText.text = $"{(i + 1) % 10}";
                slotUI.Initialize(this, i);
                slotUIs.Add(slotUI);
            }

            currentSlotCount += lineCount;
        }

        for (int i = currentSlotCount; i < inventory.inventoryData.maxSlots; i++)
        {
            GameObject obj = Instantiate(slotPrefab, slotParent);
            InventorySlotUI slotUI = obj.GetComponent<InventorySlotUI>();
            slotUI.Initialize(this, i);
            slotUIs.Add(slotUI);
        }

        playerInventory.sizeDelta = new Vector2(slotParent.sizeDelta.x, slotParent.sizeDelta.y + slotTopPanel.sizeDelta.y);
    }

    [ContextMenu("# Inventory Refresh Test")]
    public void Refresh()
    {
        Log.UI($"UI Count: {slotUIs.Count}");
        for (int i = 0; i < slotUIs.Count; i++)
        {
            if (i < inventory.inventoryData.slots.Count)
                slotUIs[i].Set(inventory.inventoryData.slots[i]);
            else
                slotUIs[i].Clear();
        }

        Log.UI("Refresh!");
    }

    public void DropToTrash()
    {
        if (!DragController.Instance.IsDragging())
            return;

        DragController.Instance.Clear();
    }
}
