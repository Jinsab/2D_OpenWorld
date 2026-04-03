using System.Collections.Generic;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.04.03 오후 16:25
 *  마지막 수정 일자 : 26.04.03 오후 16:25
 *  
 *  [스크립트 목적 및 내용]
 *  1. 인벤토리 시스템 - 장비 인벤토리의 순수한 값 (데이터)
 *    1-1. 플레이어 장비 인벤토리 전용 데이터 클래스입니다.
 *      
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

[System.Serializable]
public class EquipmentSaveData : InventorySlot
{
    public EquipmentSlot slotType; // 부위 (Head, Ring 등)
    public int subIndex;          // 부위 내 인덱스 (0, 1 등)

    // 생성자
    public EquipmentSaveData(int id, int amt, EquipmentSlot type, int idx)
    {
        this.itemId = id;
        this.amount = amt;
        this.slotType = type;
        this.subIndex = idx;
    }
}

[System.Serializable]
public class EquipmentInventoryData
{
    // JSON 등으로 저장될 실제 리스트
    public List<EquipmentSaveData> savedSlots = new List<EquipmentSaveData>();
}
