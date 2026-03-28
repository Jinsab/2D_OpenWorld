using System.Collections;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.28 오후 21:03
 *  마지막 수정 일자 : 26.03.28 오후 21:07
 *  
 *  [스크립트 목적 및 내용]
 *  1. 배경음악 매니저
 *    1-1. BGM 크로스페이드(Crossfade) 시스템
 *         - 배경음악이 바뀔 때 뚝 끊기지 않고,
 *           A 트랙은 서서히 작아지고(Fade Out) B 트랙은 서서히 커지는(Fade In) 시스템
 *    1-2. 배경음악이므로 기본적으로 loop = true 설정을 유지
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
