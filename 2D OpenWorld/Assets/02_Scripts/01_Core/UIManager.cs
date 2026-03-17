using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.16 오후 16:50
 *  마지막 수정 일자 : 26.03.16 오후 18:57
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
    public static UIManager Instance;

    [Header("UI References")]
    [SerializeField] private GameObject inventoryHotbarWindow;
    [SerializeField] private GameObject inventoryWindow;
    [SerializeField] private GameObject storageWindow;
    [SerializeField] private GameObject statWindow;
    [SerializeField] private GameObject craftingWindow;
    [SerializeField] private GameObject settingsWindow;

    [Header("Input System")]
    [SerializeField] private PlayerInput playerInput;

    // 현재 열려 있는 UI들을 관리하는 스택
    private Stack<GameObject> uiStack = new Stack<GameObject>();

    private void Awake()
    {
        Instance = this;

        // 시작 시 모든 UI 비활성화
        CloseAll();
    }

    // --- 핵심 로직: UI 토글 ---
    public void ToggleInventory() => ToggleWindow(inventoryWindow);
    public void ToggleSettings() => ToggleWindow(settingsWindow);

    private void ToggleWindow(GameObject window)
    {
        if (window.activeSelf)
        {
            CloseWindow(window);
        }
        else
        {
            OpenWindow(window);
        }
    }

    public void OpenWindow(GameObject window)
    {
        window.SetActive(true);
        if (!uiStack.Contains(window))
        {
            uiStack.Push(window);
        }

        UpdateInputState();
    }

    public void CloseWindow(GameObject window)
    {
        window.SetActive(false);

        // 스택에서 해당 윈도우 제거 (중간에 있는 창을 닫을 경우 대비)
        // 실제 스택은 중간 제거가 안 되므로 리스트를 쓰거나 팝업 순서 정립 필요
        UpdateInputState();
    }

    // --- 입력 상태 관리 ---
    private void UpdateInputState()
    {
        // 스택에 UI가 하나라도 있으면 UI 모드, 없으면 Player 모드
        if (uiStack.Count > 0)
        {
            playerInput.SwitchCurrentActionMap("UI");
            //Cursor.lockState = CursorLockMode.None;
            //Cursor.visible = true;
        }
        else
        {
            playerInput.SwitchCurrentActionMap("Player");
            //Cursor.lockState = CursorLockMode.Locked;
            //Cursor.visible = false;
        }
    }

    public void OnESCPressed()
    {
        // 1. 열린 UI가 있다면 가장 최근 것부터 닫음
        if (uiStack.Count > 0)
        {
            GameObject top = uiStack.Pop();
            top.SetActive(false);
            UpdateInputState();
        }
        // 2. 열린 UI가 없다면 설정창을 엶
        else
        {
            ToggleSettings();
        }
    }

    private void CloseAll()
    {
        inventoryWindow.SetActive(false);
        storageWindow.SetActive(false);
        // ... 모든 참조 UI 비활성화
        uiStack.Clear();
        UpdateInputState();
    }
}
