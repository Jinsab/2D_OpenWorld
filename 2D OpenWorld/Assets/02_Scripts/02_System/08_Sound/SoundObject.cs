using System.Collections;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.27 오후 16:14
 *  마지막 수정 일자 : 26.03.27 오후 16:55
 *  
 *  [스크립트 목적 및 내용]
 *  1. 사운드 풀의 개별 유닛
 *    1-1. 
 *  
 *  2. 추후 고려 사항
 *    2-1. 
 *    
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class SoundObject : MonoBehaviour
{
    [SerializeField] private AudioSource source;
    public bool IsUsing { get; private set; }

    public void Play(AudioClip clip, SoundDatabase.SoundData data, Vector3? pos)
    {
        IsUsing = true;
        gameObject.SetActive(true);

        if (pos.HasValue)
        {
            transform.position = pos.Value;
            source.spatialBlend = 1.0f; // 3D
        }
        else
        {
            source.spatialBlend = 0f; // 2D
        }

        source.clip = clip;
        source.volume = data.volume;
        source.pitch = data.pitch + Random.Range(-0.05f, 0.05f);
        source.loop = data.loop;
        source.Play();

        if (!data.loop) StartCoroutine(AutoReturn(clip.length));
    }

    private IEnumerator AutoReturn(float delay)
    {
        yield return new WaitForSeconds(delay + 0.1f);
        StopAndReturn();
    }

    public void StopAndReturn()
    {
        source.Stop();
        IsUsing = false;
        gameObject.SetActive(false);
    }
}
