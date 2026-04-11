using UnityEngine;
using UnityEngine.InputSystem;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.22 오후 19:20
 *  마지막 수정 일자 : 26.03.22 오후 21:51
 *  
 *  [스크립트 목적 및 내용]
 *  1. 플레이어 UI 관리 스크립트
 *    1-1. Action Map - Player와 UI 각각에 맞게 이벤트를 연결함
 *    1-2. UIManager의 함수를 키 입력에 따라 직접적으로 호출함
 *     
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class PlayerUI : MonoBehaviour
{
    // Player Controller
    private PlayerController Player;

    // Player Input System
    private InputAction playerTabAction; // Tab: 인벤토리 + 장비 + 능력치 + 기본제작
    private InputAction playerEscAction; // ESC: 환경설정
    private InputAction playerMapAction; // M: 미니맵 오픈

    private InputAction uiTabAction;
    private InputAction uiEscAction;
    private InputAction uiMapAction;

    public void Initialize()
    {
        Player = GetComponent<PlayerController>();

        playerTabAction = Player.Input.actions.FindAction("Player/Inventory");
        playerEscAction = Player.Input.actions.FindAction("Player/Setting");
        playerMapAction = Player.Input.actions.FindAction("Player/Map");
        uiTabAction = Player.Input.actions.FindAction("UI/Inventory");
        uiEscAction = Player.Input.actions.FindAction("UI/Setting");
        uiMapAction = Player.Input.actions.FindAction("UI/Map");

        // [Tab] 인벤토리 이벤트
        playerTabAction.performed += ctx =>
        {
            //Log.UI("Player Tab Key: 인벤토리 키 입력");
            UIManager.Instance.ChangeState(UIManager.UIState.CharacterInfo);
        };
        uiTabAction.performed += ctx =>
        {
            //Log.UI("UI Tab Key: 인벤토리 키 입력");
            UIManager.Instance.ChangeState(UIManager.UIState.CharacterInfo);
        };

        // [ESC] 설정 이벤트
        playerEscAction.performed += ctx =>
        {
            //Log.UI("Player Esc Key: 설정 키 입력");
            UIManager.Instance.HandleESC();
        };
        uiEscAction.performed += ctx =>
        {
            //Log.UI("UI Esc Key: 설정 키 입력");
            UIManager.Instance.HandleESC();
        };

        // [M] 미니맵 이벤트
        playerMapAction.performed += ctx =>
        {
            Log.UI("Player M Key: 미니맵 키 입력");
        };
        uiMapAction.performed += ctx =>
        {
            Log.UI("UI M Key: 미니맵 키 입력");
        };
    }
}
