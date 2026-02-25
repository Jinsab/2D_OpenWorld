using System;

namespace Arawn.CrystalSave.Runtime
{
        /// <summary>
        /// Marks a <see cref="SaveableComponent"/> whose inspector should expose the
        /// Destroyed Object Restore section.
        /// </summary>
        [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
        public sealed class DestroyedObjectRestoreAttribute : Attribute
        {
        }
}
