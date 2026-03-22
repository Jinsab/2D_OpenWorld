using UnityEngine;
using UnityEngine.InputSystem;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.16 오후 16:50
 *  마지막 수정 일자 : 26.03.22 오후 18:44
 *  
 *  [스크립트 목적 및 내용]
 *  1. UI 매니저 스크립트
 *    1-1. 
 *     
 *  [스크립트 작성 도움 출처]
 *  1. https://dochi-programming.tistory.com/231
 *  2. https://rimugiri.tistory.com/entry/Unity-new-Input-System-%EC%82%AC%EC%9A%A9%EB%B2%95
 */

public class UIManager : MonoBehaviour
{
    public enum UIState
    {
        None,           // 평상시 (HUD만 노출)
        CharacterInfo,  // Tab: 인벤토리 + 장비 + 능력치 + 기본제작
        Chest,          // E(상자): 인벤토리 + 상자 내용물
        WorkBench,      // E(작업대): 인벤토리 + 특정 제작 레시피
        Settings        // ESC: 환경설정
    }

    public static UIManager Instance;
    private UIState currentState = UIState.None;
    public UIState CurrentState => currentState;

    [Header("UI References")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private GameObject storagePanel;
    [SerializeField] private GameObject storageButtonPanel;
    [SerializeField] private GameObject informationPanel;
    [SerializeField] private GameObject baseCraftingPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Input System")]
    [SerializeField] private PlayerInput playerInput;

    private void Awake()
    {
        Instance = this;

        // 시작 시 모든 UI 비활성화
        ForceCloseAll();
    }

    // --- 핵심 함수: 상태 전환 ---
    public void ChangeState(UIState newState)
    {
        // 1. 현재 상태와 같으면 닫기 (토글 기능)
        if (currentState == newState)
        {
            ForceCloseAll();
            return;
        }

        // 2. 기존 패널 모두 비활성화
        DisableAllPanels();

        // 3. 새 상태에 따른 패널 그룹 활성화
        currentState = newState;
        ApplyStateLayout(newState);

        // 4. 입력 맵 및 커서 상태 업데이트
        UpdateInputAndCursor();
    }

    private void ApplyStateLayout(UIState state)
    {
        switch (state)
        {
            case UIState.CharacterInfo:
                inventoryPanel.SetActive(true);
                informationPanel.SetActive(true);
                baseCraftingPanel.SetActive(true);
                break;

            case UIState.Chest:
                inventoryPanel.SetActive(true);
                storageButtonPanel.SetActive(true);
                storagePanel.SetActive(true);
                break;

            case UIState.WorkBench:
                inventoryPanel.SetActive(true);
                // workbenchPanel.SetActive(true); // 확장 가능
                break;

            case UIState.Settings:
                settingsPanel.SetActive(true);
                break;

            case UIState.None:
                // HUD 외 모두 비활성 유지
                break;
        }
    }

    // --- 유틸리티 함수 ---
    private void UpdateInputAndCursor()
    {
        if (currentState == UIState.None)
        {
            playerInput.SwitchCurrentActionMap("Player");
            //Cursor.lockState = CursorLockMode.Locked;
            //Cursor.visible = false;
        }
        else
        {
            playerInput.SwitchCurrentActionMap("UI");
            //Cursor.lockState = CursorLockMode.None;
            //Cursor.visible = true;
        }
    }

    public void ForceCloseAll()
    {
        currentState = UIState.None;
        DisableAllPanels();
        UpdateInputAndCursor();
    }

    private void DisableAllPanels()
    {
        inventoryPanel.SetActive(false);
        informationPanel.SetActive(false);
        baseCraftingPanel.SetActive(false);
        storagePanel.SetActive(false);
        storageButtonPanel.SetActive(false);
        settingsPanel.SetActive(false);
    }

    // --- ESC 키 전용 로직 ---
    public void HandleESC()
    {
        if (currentState == UIState.None)
            ChangeState(UIState.Settings);
        else
            ForceCloseAll();
    }
}
