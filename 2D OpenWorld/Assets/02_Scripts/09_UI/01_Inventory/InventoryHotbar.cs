using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.26 오후 16:58
 *  마지막 수정 일자 : 26.03.26 오후 16:59
 *  
 *  [스크립트 목적 및 내용]
 *  1. 인벤토리 핫바(퀵슬롯) UI
 *    1-1. 현재 인벤토리 UI의 슬롯 데이터에 따라 아이템을 핫바에서 표기
 *    1-2. 인벤토리 UI에 이미 슬롯 데이터가 존재하므로, 해당 데이터를 가공하여 사용
 *    1-3. 핫바의 1줄은 10칸을 의미함.
 *    1-4. 핫바는 항상 표기되는 상태임
 *    1-5. 핫바의 아이템 슬롯을 클릭하여 해당 슬롯으로 이동할 수 있음
 *    1-6. 또는, 퀵슬롯 버튼 1~0번을 눌러 해당 슬롯으로 이동할 수 있음
 *    
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class InventoryHotbar : MonoBehaviour
{
    [Header(" # Player Inventory Data")]
    public Inventory inventory;
    public InventoryUI inventoryUI; // inventoryUI.lineCount == 라인 별 슬롯 수
    public InventorySlotUI[] hotbarSlots; // 핫바 슬롯 배열 (1줄 10칸)

    // 핫바는 항상 1줄이므로,
    // inventoryUI.lineCount로 슬롯 라인 수를 계산하여
    // 해당 라인에 있는 슬롯만 핫바에 표기하면 됨
    private int slotLineCount = 0;

    private void SlotUpdate()
    {
        // inventoryUI의 슬롯 데이터 중,
        // slotLineCount에 해당하는 라인에 있는 슬롯만 핫바에 표기

        // 예시)
        // slotLineCount가 0이면, inventoryUI의 0~9번 슬롯이 핫바에 표기
        // slotLineCount가 1이면, inventoryUI의 10~19번 슬롯이 핫바에 표기

        // 고려해야 할 사항
        // 1. 슬롯 라인 수 계산 (슬롯 라인 수는 inventoryUI.lineCount로 계산 가능)
        // 2. 슬롯 라인 수에 따른 슬롯 인덱스 계산
        //    - 이 때, 딱 떨어지는 경우가 아닐 수도 있음
        // 3. 슬롯 데이터에 따른 아이템 표기

    }

    // 슬롯 번호 1~0번을 눌렀을 때, 해당 슬롯으로 이동하는 함수
    public void SlotMove(int slotIndex)
    {
        // slotIndex는 0~9번으로 입력받음
        // slotLineCount에 따른 슬롯 인덱스 계산
        // 예시: slotLineCount가 0이면, slotIndex는 0~9번 슬롯을 의미
        //      slotLineCount가 1이면, slotIndex는 10~19번 슬롯을 의미
        // 고려해야 할 사항
        // 1. 슬롯 라인 수 계산 (슬롯 라인 수는 inventoryUI.lineCount로 계산 가능)
        // 2. 슬롯 라인 수에 따른 슬롯 인덱스 계산
        //    - 이 때, 딱 떨어지는 경우가 아닐 수도 있음
        // 3. 슬롯 데이터에 따른 아이템 표기
    }

    // 퀵슬롯 목록을 다음 라인으로 이동하는 함수
    private void SlotLineUp()
    {
        int line = SetLineCount();
        slotLineCount = (slotLineCount + 1) > line ? 0 : slotLineCount++;
    }

    // 퀵슬롯 목록을 이전 라인으로 이동하는 함수
    private void SlotLineDown()
    {
        int line = SetLineCount();
        slotLineCount = (slotLineCount - 1) < 0 ? line : slotLineCount--;
    }

    private int SetLineCount()
    {
        // 예시: 20 / 10 = 2줄 (사용하는 데이터는 2줄이라면 0, 1번의 슬롯이 필요로 2줄)
        // 슬롯 라인 수는 최대 슬롯 수 / 라인 별 슬롯 수로 계산할 수 있음
        // 이 때, 딱 떨어지는 경우가 아닐 수도 있으므로, 올림으로 계산해야 함
        return Mathf.CeilToInt(inventory.inventoryData.maxSlots / inventoryUI.lineCount);
    }
}
