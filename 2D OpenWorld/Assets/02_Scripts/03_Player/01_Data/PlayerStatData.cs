using System.Collections.Generic;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.20 오전 01:37
 *  마지막 수정 일자 : 26.03.06 오후 14:59
 *  
 *  [스크립트 목적 및 내용]
 *  1. 플레이어 스탯 관리
 *    1-1. 
 *     
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

[System.Serializable]
public struct StatEntry
{
    public StatType statType;
    public CharacterStat stat;

    public StatEntry(StatType type, CharacterStat stat)
    {
        statType = type;
        this.stat = stat;
    }
}

public class PlayerStatData
{
    public List<StatEntry> Stats = new List<StatEntry>();
}
