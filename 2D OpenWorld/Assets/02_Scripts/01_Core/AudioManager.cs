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
 *  1. 사운드 매니저
 *    1-1. 
 *  
 *  2. 추후 고려 사항
 *    2-1. 최대 개수 제한 (Cap Limit):
 *         - 무한정 생성되지 않도록 카테고리 별(예: SFX 20개, UI 5개)
 *         - 최대 동시 재생 개수를 제한해야 합니다.
 *    2-2. 우선순위 (Priority):
 *         - 풀이 가득 찼을 때, 덜 중요한 소리(멀리 있는 발소리)를 끊고
 *         - 중요한 소리(플레이어 피격음)를 재생하는 로직이 필요합니다.
 *    2-3. 3D 사운드 해제:
 *         - 재생이 끝난 AudioSource는 위치를 초기화하거나
 *         - 플레이어를 따라다니는 스크립트를 중지시켜야 합니다.
 *    2-4. 자동 반환:
 *         - Clip의 길이를 체크하거나 코루틴을 사용하여 재생 완료 후
 *         - 자동으로 '사용 가능' 상태로 전환해야 합니다.
 *    
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [SerializeField] private SoundDatabase db;
    [SerializeField] private GameObject soundObjectPrefab;
    [SerializeField] private int initialPoolSize = 20;

    private List<SoundObject> pool = new List<SoundObject>();

    private void Awake()
    {
        Instance = this;
        for (int i = 0; i < initialPoolSize; i++) AddToPool();
    }

    private SoundObject AddToPool()
    {
        GameObject obj = Instantiate(soundObjectPrefab, transform);
        SoundObject so = obj.GetComponent<SoundObject>();
        so.gameObject.SetActive(false);
        pool.Add(so);
        return so;
    }

    // 이제 string 대신 SND enum을 사용하여 오타 방지!
    //public void Play(SND soundID, Vector3? position = null)
    //{
    //    if (soundID == SND.None) return;

    //    var data = db.soundEntries.Find(s => s.soundID == soundID.ToString());
    //    if (data == null) return;

    //    AudioClip clip = data.GetRandomClip();
    //    if (clip == null) return;

    //    SoundObject so = pool.Find(s => !s.IsUsing) ?? AddToPool();
    //    so.Play(clip, data, position);
    //}
}
