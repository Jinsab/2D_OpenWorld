using UnityEngine;
using UnityEngine.InputSystem;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.22 오후 19:20
 *  마지막 수정 일자 : 26.03.22 오후 19:20
 *  
 *  [스크립트 목적 및 내용]
 *  1. 플레이어 UI 관리 스크립트
 *    1-1. 
 *     
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class PlayerUI : MonoBehaviour
{
    // Player Controller
    private PlayerController Player;

    // Player Input System
    private InputAction tabAction; // Tab: 인벤토리 + 장비 + 능력치 + 기본제작
    private InputAction escAction; // ESC: 환경설정
    private InputAction mapAction; // M: 미니맵 오픈

    public void Initialize()
    {
        Player = GetComponent<PlayerController>();

        tabAction = Player.Input.actions["Inventory"];
        escAction = Player.Input.actions["Setting"];
        mapAction = Player.Input.actions["Map"];

        // [Tab] 인벤토리 이벤트
        tabAction.performed += ctx =>
        {
            Log.UI("Tab Key: 인벤토리 패널 오픈");
            UIManager.Instance.ChangeState(UIManager.UIState.CharacterInfo);
        };

        // [ESC] 설정 이벤트
        escAction.performed += ctx =>
        {
            Log.UI("Esc Key: 설정 패널 오픈");
            UIManager.Instance.ChangeState(UIManager.UIState.Settings);
        };

        // [M] 미니맵 이벤트
        mapAction.performed += ctx =>
        {
            Log.UI("M Key: 미니맵 패널 오픈");
        };
    }
}
