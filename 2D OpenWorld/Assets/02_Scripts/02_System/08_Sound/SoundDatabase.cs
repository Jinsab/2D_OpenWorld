using System.Collections.Generic;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.26 오후 14:54
 *  마지막 수정 일자 : 26.03.26 오후 22:22
 *  
 *  [스크립트 목적 및 내용]
 *  1. 사운드 데이터베이스
 *    1-1. 사운드 키 명명 규칙
 *         - Category_Action_Detail 형식
 *         - 예시) Combat_Sword_Swing, Env_Rain_Light
 *  
 *  2. 추후 고려 사항
 *    2-1. 환경음과 배경음의 Crossfade
 *         - 배경음이 바뀔 때 뚝 끊기지 않고 자연스럽게 교체되려면,
 *           두 개의 AudioSource를 두고 하나의 볼륨은 낮추고
 *           다른 하나는 높이는 크로스페이드(Crossfade) 로직을
 *           AudioManager에 추가해야 합니다.
 *    2-2. Addressables 연동
 *         - 사운드 파일은 용량이 크기 때문에 SoundData에 AudioClip 대신
 *           AssetReference를 두어, 실제 재생할 때만 메모리에 로드하고
 *           재생이 끝나면 해제하는 방식을 고려하세요.
 *    
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public enum SoundCategory
{
    UI,         // 버튼, 메뉴 전환
    Item,       // 획득, 버리기, 쓰레기통
    Interaction, // 문, 상자 열기
    Combat,     // 휘두르기, 피격
    System,     // 레벨업, 사망
    Ambient,    // 바람, 비 (루프물)
    BGM         // 배경음 (루프물)
}

[CreateAssetMenu(fileName = "SoundDatabase", menuName = "Audio/SoundDatabase")]
public class SoundDatabase : ScriptableObject
{
    [System.Serializable]
    public class SoundData
    {
        public string soundID; // 이 문자열이 Enum의 이름이 됩니다.
        public SoundCategory category;
        public AudioClip[] clips;
        [Range(0, 1)] public float volume = 1f;
        [Range(0.5f, 1.5f)] public float pitch = 1f;
        public bool loop;

        public AudioClip GetRandomClip() => clips.Length > 0 ? clips[Random.Range(0, clips.Length)] : null;
    }

    public List<SoundData> soundEntries = new List<SoundData>();
}
