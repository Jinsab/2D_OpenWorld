using UnityEngine;
using System.Collections.Generic;
using TMPro;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.15 오후 21:06
 *  마지막 수정 일자 : 26.02.17 오후 16:03
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
 *         └─ DragController (마우스 드래그 전담)
 *      
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class InventoryUI : MonoBehaviour
{
    [Header("# Inventory Data")]
    public Inventory inventory;    // 인벤토리 데이터
    public TMP_Text inventoryName; // 인벤토리 이름
    public int lineCount = 10;     // 1줄 당
    public bool isPlayer = false;  // 플레이어

    [Header("# Slot Prefab")]
    public GameObject slotPrefab;
    public GameObject dropZone;

    [Header("# Slot Parent")]
    public Transform slotParent;

    private List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();

    private void Awake()
    {
        // 플레이어 인벤토리라면 쓰레기통 기능을 활성화함
        if (isPlayer)
            dropZone.SetActive(true);
    }

    private void Start()
    {
        CreateSlots();
        Refresh();
    }

    void CreateSlots()
    {
        // 한 줄이 n인 인벤토리가 있다면, 최대 슬롯 / n칸 만큼의 줄 수가 필요함
        int line = Mathf.CeilToInt((float)inventory.maxSlots / lineCount);

        // Width(가로)와 Height(세로) 계산하기
        // 1. 슬롯 1개의 크기는 100x100임
        // 2. 왼쪽/오른쪽/하단에 Border 수치는 각 20임
        // 3. 상단 Border 및 각 슬롯의 Spacing은 10임
        // 4. Width의 값은 (1줄의 슬롯 개수 * 100) + ((1줄의 슬롯 개수 - 1) * 10 + 40이 됨
        // 5. Height의 값은 (최대 슬롯 / 1줄의 n칸) * 110 + 20이 됨
        for (int i = 0; i < inventory.maxSlots; i++)
        {
            GameObject obj = Instantiate(slotPrefab, slotParent);
            InventorySlotUI slotUI = obj.GetComponent<InventorySlotUI>();
            slotUI.Initialize(this, i);
            slotUIs.Add(slotUI);
        }
    }

    public void Refresh()
    {
        for (int i = 0; i < slotUIs.Count; i++)
        {
            if (i < inventory.slots.Count)
                slotUIs[i].Set(inventory.slots[i]);
            else
                slotUIs[i].Clear();
        }
    }

    public void DropToTrash()
    {
        if (!DragController.Instance.IsDragging())
            return;

        DragController.Instance.Clear();
    }
}
