using NatureBackgroundsPixelArt;
using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.26 오후 16:58
 *  마지막 수정 일자 : 26.04.10 오후 17:42
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
    [Header("# References")]
    [SerializeField] private Inventory playerInventory;
    private InputAction playerQuickSlot; // 1~0: 퀵슬롯 이동
    // [SerializeField] private QuickSlotUI quickSlotUI; // UI 표현 담당

    [Header("# Settings")]
    private int currentSelectedIndex = 0;
    private const int SLOT_COUNT = 10;

    public event Action<int> OnSlotSelected; // 선택된 인덱스가 바뀔 때 발생

    private void Start()
    {
        PlayerController player = GameManager.Instance.Player.GetComponent<PlayerController>();
        
        playerQuickSlot = player.Input.actions.FindAction("Player/QuickSlot");
        playerQuickSlot.performed += ctx =>
        {
            int.TryParse(ctx.control.name, out int number);

            if (number == 0)
            {
                number = 10; // 0 키는 10번 슬롯으로 간주
            }

            Log.UI($"Player Quick Slot Key: {number} 키 입력");
        };
    }

    private void Update()
    {
        HandleWheelInput();
        HandleNumberInput();
    }

    private void HandleWheelInput()
    {
        float wheel = Mouse.current.scroll.ReadValue().y;

        if (wheel == 0f)
            return;

        // 휠 방향에 따라 인덱스 변경 (순환 구조)
        if (wheel > 0f) currentSelectedIndex--;
        else currentSelectedIndex++;

        if (currentSelectedIndex < 0) currentSelectedIndex = SLOT_COUNT - 1;
        if (currentSelectedIndex >= SLOT_COUNT) currentSelectedIndex = 0;

        OnSlotSelected?.Invoke(currentSelectedIndex);
    }

    private void HandleNumberInput()
    {
        // 숫자키 1~9는 0~8 인덱스, 0은 9번 인덱스
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            KeyCode key = (i == 9) ? KeyCode.Alpha0 : KeyCode.Alpha1 + i;
            if (Input.GetKeyDown(key))
            {
                currentSelectedIndex = i;
                OnSlotSelected?.Invoke(currentSelectedIndex);
                break;
            }
        }
    }

    public InventorySlot GetSelectedSlot()
    {
        // 인벤토리의 0~9번 슬롯 데이터를 직접 참조
        return playerInventory.inventoryData.slots[currentSelectedIndex];
    }
}
