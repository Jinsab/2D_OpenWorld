#if ARAWN_REMEMBERME && MEMORYPACK
#if REMEMBERME_GC2INVENTORY_PRESENT
using GameCreator.Runtime.Inventory;
#endif
#if REMEMBERME_GC2MELEE_PRESENT
using GameCreator.Runtime.Melee;
#endif
#if REMEMBERME_GC2QUESTS_PRESENT
using GameCreator.Runtime.Quests;
#endif
#if REMEMBERME_GC2SHOOTER_PRESENT
using GameCreator.Runtime.Shooter;
#endif
#if REMEMBERME_GC2STATS_PRESENT
using GameCreator.Runtime.Stats;
#endif
using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	public static class TypedObjectFactory
	{
		private static readonly Dictionary<Type, Func<object, TypedObject>> TypeToWrapper = new()
		{
			{ typeof(int), obj => new IntWrapper((int)obj) },
			{ typeof(string), obj => new StringWrapper((string)obj) },
			{ typeof(bool), obj => new BoolWrapper((bool)obj) },
			{ typeof(GameObject), obj => CreateGameObjectWrapper((GameObject)obj) },
			{ typeof(double), obj => new DoubleWrapper((double)obj) },
			{ typeof(Texture2D), obj => new Texture2DWrapper((Texture2D)obj) },
			{ typeof(Texture), obj => new TextureWrapper((Texture)obj) },
			{ typeof(Color), obj => new ColorWrapper((Color)obj) },
			{ typeof(Vector2), obj => new Vector2Wrapper((Vector2)obj) },
			{ typeof(Vector3), obj => new Vector3Wrapper((Vector3)obj) },
			{ typeof(float), obj => new FloatWrapper((float)obj) },
			{ typeof(AudioClip), obj => new AudioClipWrapper((AudioClip)obj) },
			{ typeof(AnimationClip), obj => new AnimationWrapper((AnimationClip)obj) },
                        { typeof(Sprite), obj => new SpriteWrapper((Sprite)obj) },
                        { typeof(Material), obj => new MaterialWrapper((Material)obj) },
                        { typeof(Transform), obj => new TransformWrapper((Transform)obj) },
#if REMEMBERME_GC2MELEE_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK
			{ typeof(MeleeWeapon), obj => new MeleeWeaponWrapper((MeleeWeapon)obj) },
			{ typeof(Skill), obj => new SkillWrapper((Skill)obj) },
			{ typeof(Shield), obj => new ShieldWrapper((Shield)obj) },
#endif
#if REMEMBERME_GC2SHOOTER_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK
			{ typeof(ShooterWeapon), obj => new ShooterWeaponWrapper((ShooterWeapon)obj) },
#endif
#if REMEMBERME_GC2QUESTS_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK
			{ typeof(Quest), obj => new QuestWrapper((Quest)obj) },
#endif
#if REMEMBERME_GC2INVENTORY_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK
			{ typeof(Item), obj => new ItemWrapper((Item)obj) },
#endif
#if REMEMBERME_GC2STATS_PRESENT && ARAWN_REMEMBERME && REMEMBERME_GC2MODULE_PRESENT && MEMORYPACK
			{ typeof(GameCreator.Runtime.Stats.Attribute), obj => new AttributeWrapper((GameCreator.Runtime.Stats.Attribute)obj) },
			{ typeof(Stat), obj => new StatWrapper((Stat)obj) },
			{ typeof(StatusEffect), obj => new StatusEffectWrapper((StatusEffect)obj) },
			{ typeof(Formula), obj => new FormulaWrapper((Formula)obj) },
#endif
			// Add more types here as needed
		};

                public static TypedObject CreateTypedObject(object value)
                {
                        if (value == null) return null;

                        var type = value.GetType();

                        if (TypeToWrapper.TryGetValue(type, out var factory))
                        {
                                return factory(value);
                        }

                        if (type.IsEnum)
                        {
                                return new EnumWrapper((Enum)value);
                        }

                        if (value is Transform transform)
                        {
                                return new TransformWrapper(transform);
                        }

                        if (value is IList list)
                        {
                                return new ListWrapper(list);
                        }

                        if (value is IDictionary dictionary)
                        {
                                return new DictionaryWrapper(dictionary);
                        }

                        Logger.Log($"Unsupported type '{type}' during serialization. This won't break your save; it just indicates the value can't be stored.", LogLevel.Warning);
                        return null;
                }

		private static TypedObject CreateGameObjectWrapper(GameObject obj)
		{
			string uniqueID = GameObjectUtilities.GetUniqueID(obj);
			if (!string.IsNullOrEmpty(uniqueID))
			{
				return new GameObjectWrapper(uniqueID);
			}

			Logger.Log($"TypedObjectFactory: GameObject '{obj.name}' could not be serialized due to missing UniqueID.", LogLevel.Warning);
			return null;
		}
	}
}
#endif