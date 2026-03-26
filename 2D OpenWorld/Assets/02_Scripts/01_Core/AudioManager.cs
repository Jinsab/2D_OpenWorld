using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

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
 *    1-1. 
 *  
 *  2. 추후 고려 사항
 *    2-1. 
 *    
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class AudioManager: MonoBehaviour
{
    public static AudioManager Instance;
    [SerializeField] private SoundDatabase db;
    [SerializeField] private AudioMixer audioMixer; // UI, SFX, BGM 그룹 관리

    private List<AudioSource> sfxPool = new List<AudioSource>();

    public void Play(string soundID, Vector3? position = null)
    {
        var data = db.GetSound(soundID);
        if (data == null || data.clips.Length == 0) return;

        AudioClip clip = data.clips[Random.Range(0, data.clips.Length)];
        AudioSource source = GetAvailableSource();

        // 설정 적용
        source.clip = clip;
        source.volume = data.volume;
        source.pitch = data.pitch + Random.Range(-0.05f, 0.05f); // 미세한 변주
        source.loop = data.loop;

        if (position.HasValue) // 3D 사운드 (전투, 환경음 등)
        {
            source.spatialBlend = 1.0f;
            source.transform.position = position.Value;
        }
        else // 2D 사운드 (UI, 시스템)
        {
            source.spatialBlend = 0f;
        }

        source.Play();
    }

    private AudioSource GetAvailableSource()
    {
        // 풀에서 사용 중이지 않은 소스 반환 로직...
        return sfxPool.Find(s => !s.isPlaying) ?? CreateNewSource();
    }

    private AudioSource CreateNewSource()
    {

    }
}