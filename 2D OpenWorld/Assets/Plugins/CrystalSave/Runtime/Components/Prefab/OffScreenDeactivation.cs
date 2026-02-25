// OffScreenDeactivation.cs
namespace Arawn.CrystalSave.Runtime
{
	/// <summary>Bit-mask that replaces four independent bools.</summary>
	[System.Flags]
	public enum OffScreenDeactivation
	{
		None = 0,
		Colliders = 1 << 0,
		Renderers = 1 << 1,
		CharacterController = 1 << 2,
		RigidbodyKinematic = 1 << 3
	}
}
