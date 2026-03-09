using Arawn.CrystalSave.Runtime;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.06 오후 14:56
 *  마지막 수정 일자 : 26.03.06 오후 17:48
 *  
 *  [스크립트 목적 및 내용]
 *  1. 캐릭터 데이터 저장/불러오기 시스템
 *    1-1. 플레이어 최종 데이터 정보 저장
 *    1-2. 이는 유지되어, 실제 데이터로써 저장 및 불러오기로 넘겨주어야 함
 *      
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public sealed class RememberCharacterData : SaveableComponent
{
    public CharacterDataManager CharacterDataManager;

    protected override byte[] SerializeComponentData()
    {
        Debug.Log("Save data to Json");
        Debug.Log(CharacterDataManager.characterIndex);
        
        return Serializer.Serialize(JsonUtility.ToJson(CharacterDataManager.characterDataList[CharacterDataManager.characterIndex]));
    }

    protected override void DeserializeComponentData(byte[] data)
    {
        Debug.Log("Load Json to data");

        CharacterData character =
            JsonUtility.FromJson<CharacterData>(Serializer.Deserialize<string>(data));

        CharacterDataManager.characterDataList[CharacterDataManager.characterIndex] = character;
        Debug.Log($"Load Character : {character.name}");
        Debug.Log($"Load Character Inventory Data : {character.inventoryData}");
        Debug.Log($"Load Character Stat Data : {character.statData}");
    }
}
