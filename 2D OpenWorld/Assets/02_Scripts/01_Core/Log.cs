using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.13 오후 17:00
 *  마지막 수정 일자 : 26.03.13 오후 17:21
 *  
 *  [스크립트 목적 및 내용]
 *  1. 커스텀 로그 클래스
 *    1-1. 로그 카테고리 체계
 *         - [분류][태그 예시][주요 추적 대상]
 *         - 시스템 코어 [CORE]
 *           - 초기화(Init), 싱글톤 생성, 시스템 라이프사이클
 *         - 데이터 관리 [SAVE]
 *           - JSON 직렬화/역직렬화, 로컬 저장, 암호화
 *         - 리소스 로드 [ASSET]
 *           - Addressables 로딩, 캐싱, 에셋 해제(Release)
 *         - DB 시스템 [DB]
 *           - Database 로딩, 캐싱
 *         - 플레이어/캐릭터 [CHAR]
 *           - 캐릭터 생성, 데이터 할당, 비주얼 업데이트(Sprite Lib)
 *         - UI/UX [UI]
 *           - 슬롯 갱신, 팝업 온/오프, 인벤토리 인터랙션
 *         - 인게임 [GAME]
 *           - 전투, 상호작용, 시간 흐름, 서바이벌 수치
 *               
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public static class Log
{
    // Bleu de France
    public static void Core(string msg) => Debug.Log($"<color=#3498db>[CORE]</color> {msg}");
    // Renewable Energy
    public static void Save(string msg) => Debug.Log($"<color=#2ecc71>[SAVE]</color> {msg}");
    // Uranium
    public static void Asset(string msg) => Debug.Log($"<color=#f1c40f>[ASSET]</color> {msg}");
    // Light Aqua
    public static void DB(string msg) => Debug.Log($"<color=#cdf7f7>[DB]</color> {msg}");
    // Deep Lilac
    public static void Char(string msg) => Debug.Log($"<color=#9b59b6>[CHAR]</color> {msg}");
    // Frost Pink
    public static void UI(string msg) => Debug.Log($"<color=#e6b3dc>[UI]</color> {msg}");
    // Natural Cream
    public static void Game(string msg) => Debug.Log($"<color=#e6dcbe>[GAME]</color> {msg}");

    // 에러는 공용으로 사용
    public static void Error(string tag, string msg) => Debug.LogError($"[{tag}] {msg}");
}
