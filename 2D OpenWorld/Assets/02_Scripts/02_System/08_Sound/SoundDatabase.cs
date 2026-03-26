using System.Collections.Generic;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.26 오후 14:54
 *  마지막 수정 일자 : 26.03.26 오후 21:56
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

public enum SoundType
{
    Metal,
    Wood,
    Fabric
}

public enum ItemSoundState
{
    Pickup,
    Drop,
    Trash
}

[CreateAssetMenu(fileName = "SoundDatabase", menuName = "Database/SoundDatabase")]
public class SoundDatabase : ScriptableObject
{
    [System.Serializable]
    public class SoundGroup
    {
        public SoundType type; // Metal, Wood, Fabric...
        public AudioClip[] pickupClips; // 집을 때 (랜덤 재생용 배열)
        public AudioClip[] dropClips;   // 버릴 때
        public AudioClip[] trashClips;  // 파괴될 때
    }

    public List<SoundGroup> soundGroups;

    public AudioClip GetRandomClip(SoundType type, ItemSoundState state)
    {
        var group = soundGroups.Find(g => g.type == type);
        if (group == null) return null;

        AudioClip[] targetClips = state switch
        {
            ItemSoundState.Pickup => group.pickupClips,
            ItemSoundState.Drop => group.dropClips,
            ItemSoundState.Trash => group.trashClips,
            _ => null
        };

        if (targetClips == null || targetClips.Length == 0) return null;
        return targetClips[Random.Range(0, targetClips.Length)];
    }
}
