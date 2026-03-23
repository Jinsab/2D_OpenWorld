using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.23 오후 20:41
 *  마지막 수정 일자 : 26.03.23 오후 20:41
 *  
 *  [스크립트 목적 및 내용]
 *  1. 인벤토리 시스템 - 드래그 앤 드롭 시각화
 *    1-1. 마우스 커서를 따라다니며 현재 들고 있는 아이템을 보여주는 UI 클래스
 *    1-2. 별도의 Canvas 최상단 레이어에 배치
 *      
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class MouseSlotUI : MonoBehaviour
{
    public static MouseSlotUI Instance;

    [Header("UI Elements")]
    public Image icon;
    public TextMeshProUGUI amountText;

    // 현재 마우스가 들고 있는 실제 데이터
    public InventorySlot heldSlot = new InventorySlot();

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        // 1. 마우스 위치 추적
        transform.position = Mouse.current.position.ReadValue();

        // 2. 아이템이 있을 때만 표시
        bool hasItem = heldSlot.itemId != 0;
        icon.enabled = hasItem;
        amountText.enabled = hasItem;

        if (hasItem)
        {
            var item = ItemDatabase.Instance.GetItem(heldSlot.itemId);
            icon.sprite = item.Icon;
            amountText.SetText(item.maxStack > 1 ? heldSlot.amount.ToString() : "");
        }
    }
}
