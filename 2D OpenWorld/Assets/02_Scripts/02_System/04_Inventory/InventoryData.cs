using System.Collections.Generic;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.06 오후 14:10
 *  마지막 수정 일자 : 26.03.06 오후 14:40
 *  
 *  [스크립트 목적 및 내용]
 *  1. 인벤토리 시스템 - 인벤토리의 순수한 값 (데이터)
 *    1-1. 플레이어, 창고, 화로(용광로), 그 외 작업대 등에 입혀 사용할 수 있음
 *    1-2. 기존 인벤토리는 MonoBehaviour로 저장하기에 적합하지 않아
 *         InventoryData로 분리하여 저장할 예정입니다.
 *    
 *  2. 큰 그림
 *    - Inventory System
 *      ├─ Inventory (InventoryData를 가지는 로직, 아이템 추가, 삭제 등 가공의 역할을 함)
 *      │  └─ InventoryData (인벤토리 데이터 - 아이템 ID, 수량 등 '값'만 가집니다.) (저장 대상)
 *      │
 *      └─ Inventory UI (전체 UI 관리)
 *         ├─ InventorySlotUI (슬롯 단위 UI)
 *         ├─ DragController (마우스 드래그 전담)
 *         ├─ DragIconUI (아이템 드래그 시 아이콘 표시)
 *         └─ SlotUIInteraction (마우스 호버 시 툴팁 표시 및 하이라이트 효과)
 *      
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

[System.Serializable]
public class InventorySlot
{
    public int itemId;
    public int amount;

    public InventorySlot()
    {
        // 빈 슬롯을 나타내기 위해 itemId를 0으로 설정 (0은 아이템 없음)
        itemId = 0;
        amount = 0;
    }

    public InventorySlot(Item item, int amount)
    {
        itemId = item.itemId;
        this.amount = amount;
    }
}

[System.Serializable]
public class InventoryData
{
    [Header("# Inventory Data")]
    public List<InventorySlot> slots;

    [Header("# Inventory Settings")]
    public int maxSlots = 20;
}
