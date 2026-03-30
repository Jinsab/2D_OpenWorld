using System.Collections;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.28 오후 21:03
 *  마지막 수정 일자 : 26.03.28 오후 21:11
 *  
 *  [스크립트 목적 및 내용]
 *  1. 배경음악 매니저
 *    1-1. BGM 크로스페이드(Crossfade) 시스템
 *         - 배경음악이 바뀔 때 뚝 끊기지 않고,
 *           A 트랙은 서서히 작아지고(Fade Out) B 트랙은 서서히 커지는(Fade In) 시스템
 *    1-2. 배경음악이므로 기본적으로 loop = true 설정을 유지
 *      
 *   2. 고려사항
 *     2-1. BGM 볼륨 조절 기능과 연동 가능하도록 설계
 *     2-2. 긴 페이드 시간 설정 시, 현재 곡이 완전히 사라지기 전에 다음 곡이 최대 볼륨에 도달할 수 있으므로
 *          BGM이 자주 바뀌는 상황에서는 페이드 중복 방지 로직 필요 (예: 플레이어가 빠르게 지역 이동)
 *     2-3. 무음 전환:
 *          - 무음 전환: PlayBGM(null)을 호출하면 현재 배경음만 서서히 사라지게 하는 예외 처리를 추가하기
 *     2-4. 페이드 도중 다른 요청:
 *          - 페이드가 진행 중일 때 다시 PlayBGM이 호출되면 코루틴을 멈추고
 *            현재 볼륨 상태에서 다시 페이드를 시작해야 부자연스럽지 않습니다.
 *            (위의 StopCoroutine 로직이 그 역할을 합니다.)
 *            
 *   3. 시스템 통합 시 고려사항
 *     3-1. 초기 볼륨 설정:
 *          - Audio Mixer를 사용 중이라면 AudioSource의 출력(Output)이
 *            Music 그룹으로 연결되어 있는지 확인하기
 *            그래야 플레이어가 설정에서 배경음만 끌 수 있음
 *     3-2. 데이터베이스 연동:
 *          - SND Enum과 마찬가지로
 *            BGM 전용 Enum(BGM_Field, BGM_Boss, BGM_Town)을 만들어 관리
 *     3-3. 메모리 최적화:
 *          - 배경음악은 파일 용량이 크므로,
 *            모든 BGM을 메모리에 올려두지 말고 Addressables를 통해
 *            씬 이동 시점에 로드하는 방식을 추천
 *   
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance;

    [SerializeField] private AudioSource[] bgmSources; // 인스펙터에서 2개 할당
    [SerializeField] private float fadeDuration = 1.5f;

    private int activeSourceIndex = 0;
    private Coroutine fadeCoroutine;

    private void Awake() => Instance = this;

    public void PlayBGM(AudioClip newClip)
    {
        if (bgmSources[activeSourceIndex].clip == newClip) return;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(CrossfadeRoutine(newClip));
    }

    private IEnumerator CrossfadeRoutine(AudioClip nextClip)
    {
        int nextSourceIndex = 1 - activeSourceIndex;
        AudioSource currentSource = bgmSources[activeSourceIndex];
        AudioSource nextSource = bgmSources[nextSourceIndex];

        // 1. 다음 소스 설정 및 재생 시작 (볼륨 0에서 시작)
        nextSource.clip = nextClip;
        nextSource.volume = 0;
        nextSource.Play();

        float timer = 0;
        float startVolume = currentSource.volume;

        // 2. 교차 페이드 진행
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float percent = timer / fadeDuration;

            // 현재 곡은 작아지고, 다음 곡은 커짐
            currentSource.volume = Mathf.Lerp(startVolume, 0, percent);
            nextSource.volume = Mathf.Lerp(0, 1, percent); // 1 대신 설정된 BGM 볼륨값 사용 가능

            yield return null;
        }

        // 3. 마무리 및 인덱스 교체
        currentSource.Stop();
        currentSource.volume = 0;
        nextSource.volume = 1;

        activeSourceIndex = nextSourceIndex;
    }
}
