using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.26 오후 15:03
 *  마지막 수정 일자 : 26.03.26 오후 16:59
 *  
 *  [스크립트 목적 및 내용]
 *  1. 쓰레기통 기능
 *    1-1. InventorySlot 객체 딱 하나만 관리하는 UI 슬롯
 *    1-2. 하나의 인벤토리로써 관리됨 (아이템 합치기 등)
 *    1-3. 하지만, 인벤토리 수납 시 자동으로 수납되는 등의 기능은 제한됨
 *    1-4. 아이템을 쓰레기통 슬롯에 드롭 가능
 *    1-5. 실수로 버린 경우 다시 꺼낼 수 있도록 '마지막으로 버린 아이템' 1개만 유지
 *    1-6. 버튼 클릭 시 아이템 영구 삭제
 *      
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class TrashManager : MonoBehaviour
{
    [Header("# Inventory Data")]
    public Inventory trashCan;
    public InventorySlotUI trashSlotUI;

    private void OnEnable()
    {
        // 중요: 인벤토리의 이벤트에 내 함수(RefreshSlot)를 연결(구독)
        trashCan.OnSlotChanged += RefreshSlot;
    }

    private void OnDisable()
    {
        // 메모리 누수 방지를 위해 오브젝트가 꺼질 때 구독 해제
        trashCan.OnSlotChanged -= RefreshSlot;
    }

    // 이벤트가 발생하면 실행될 함수
    private void RefreshSlot(int index)
    {
        // 1. 인덱스 유효성 검사 (음수 방지 및 범위 체크)
        if (index < 0 || index >= trashCan.inventoryData.slots.Count)
        {
            Log.Error("UI", $"Invalid Slot Index: {index}");
            return;
        }

        // 2. 해당 인덱스의 데이터와 UI 연결
        var data = trashCan.inventoryData.slots[index];
        trashSlotUI.UpdateVisual(data);

        Log.UI($"{index}번 UI 슬롯 갱신 완료 (ID: {data.itemId}, Qty: {data.amount})");
    }

    // [비우기] 버튼을 눌렀을 때 실행
    public void ClearTrash()
    {
        if (trashCan.inventoryData.slots[0].itemId == 0)
            return;

        Log.UI($"{ItemDatabase.Instance.GetItem(trashCan.inventoryData.slots[0].itemId).itemName}이(가) 영구 삭제되었습니다.");
        trashCan.inventoryData.slots[0].Clear();
        trashSlotUI.ClearVisual();

        // 이때 "치익-" 하는 쓰레기 처리 사운드를 재생하면 좋습니다.
    }
}
