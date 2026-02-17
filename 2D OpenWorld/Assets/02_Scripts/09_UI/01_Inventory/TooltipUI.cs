using UnityEngine;
using UnityEngine.UI;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.17 오후 22:22
 *  마지막 수정 일자 : 26.02.17 오후 22:22
 *  
 *  [스크립트 목적 및 내용]
 *  1. 인벤토리 시스템 - 툴팁 UI 관리
 *    1-1. 마우스가 본인(슬롯) UI 위에 올라왔을 때 툴팁 표기
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

public class TooltipUI : MonoBehaviour
{
    public static TooltipUI Instance;

    public GameObject panel;
    public Text nameText;
    public Text descText;

    private void Awake()
    {
        Instance = this;
        Hide();
    }

    public void Show(Item item)
    {
        panel.SetActive(true);
        nameText.text = item.itemName;
        descText.text = item.itemDesc;
    }

    public void Hide()
    {
        panel.SetActive(false);
    }
}
