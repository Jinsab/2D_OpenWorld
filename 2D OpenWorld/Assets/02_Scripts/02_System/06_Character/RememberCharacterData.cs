using Arawn.CrystalSave.Runtime;
using UnityEngine;

public sealed class RememberCharacterData : SaveableComponent
{
    public CharacterDataManager CharacterDataManager;

    protected override byte[] SerializeComponentData()
    {
        Debug.Log("오류 체크 1");
        
        return Serializer.Serialize(JsonUtility.ToJson(CharacterDataManager.characterDataList[CharacterDataManager.characterIndex]));
    }

    protected override void DeserializeComponentData(byte[] data)
    {
        Debug.Log("오류 체크 2");

        CharacterData character = 
            JsonUtility.FromJson<CharacterData>(Serializer.Deserialize<string>(data));
        //Serializer.Deserialize<CharacterData>(data);

        CharacterDataManager.characterDataList[CharacterDataManager.characterIndex] = character;
        Debug.Log($"Load Character : {character.name}");

        //Debug.Log("오류 체크 3");
        //// 기존 컴포넌트를 가져오거나 없으면 추가
        //CharacterData existing = CharacterDataManager.characterDataList[CharacterDataManager.characterIndex];

        //Debug.Log("오류 체크 4");

        //if (existing == null)
        //{
        //    existing = new CharacterData();
        //}

        // 필드별로 값 복사(GetComponent<>() = character; 는 허용되지 않음)
        //existing.name
        //existing.isEmpty = character.isEmpty;
        //existing.appearanceData = character.appearanceData;
        //// existing.statData = character.statData;
        //// existing.inventory = character.inventory;
        //existing.level = character.level;
        //existing.type = character.type;
        //existing.playTime = character.playTime;

        //CharacterDataManager.characterDataList[CharacterDataManager.characterIndex] = existing; 
    }
}
