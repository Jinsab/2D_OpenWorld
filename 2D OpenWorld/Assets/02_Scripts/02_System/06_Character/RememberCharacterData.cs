using Arawn.CrystalSave.Runtime;
using UnityEngine;

public sealed class RememberCharacterData : SaveableComponent
{
    public CharacterDataManager CharacterDataManager;

    protected override byte[] SerializeComponentData()
    {
        Debug.Log("오류 체크 0");
        
        return Serializer.Serialize(CharacterDataManager.characterDataList[CharacterDataManager.characterIndex]);
    }

    protected override void DeserializeComponentData(byte[] data)
    {
        Debug.Log("오류 체크 1");
        CharacterData character = Serializer.Deserialize<CharacterData>(data);

        Debug.Log("오류 체크 2");
        // 기존 컴포넌트를 가져오거나 없으면 추가
        CharacterData existing = CharacterDataManager.Instance.characterDataList[CharacterDataManager.Instance.characterIndex];

        Debug.Log("오류 체크 3");

        if (existing == null)
        {
            existing = new CharacterData();
        }

        // 필드별로 값 복사(GetComponent<>() = character; 는 허용되지 않음)
        existing.isEmpty = character.isEmpty;
        existing.appearanceData = character.appearanceData;
        existing.statData = character.statData;
        existing.inventory = character.inventory;
        existing.level = character.level;
        existing.type = character.type;
        existing.playTime = character.playTime;
    }
}
