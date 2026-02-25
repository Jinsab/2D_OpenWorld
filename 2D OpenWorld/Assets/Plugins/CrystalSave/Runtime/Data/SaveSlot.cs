#if MEMORYPACK
using System;
using System.Collections.Generic;
using MemoryPack;

namespace Arawn.CrystalSave.Runtime
{
	[MemoryPackable]
	[Serializable]
        public partial class SaveSlot
        {
                public int SlotNumber { get; set; }
                public string SlotName { get; set; }
                public DateTime LastSaved { get; set; }
                public string ScreenshotFileName { get; set; }
                public string LastActiveScene { get; set; }

                /// <summary>
                /// Optional key/value pairs stored alongside built‑in metadata.
                /// </summary>
                public Dictionary<string, string> CustomMetadata { get; set; }

		[MemoryPackConstructor]
                public SaveSlot(int slotNumber, string slotName, DateTime lastSaved, string screenshotFileName, string lastActiveScene)
                {
                        SlotNumber = slotNumber;
                        SlotName = slotName;
                        LastSaved = lastSaved;
                        ScreenshotFileName = screenshotFileName;
                        LastActiveScene = lastActiveScene;
                        CustomMetadata = new Dictionary<string, string>();
                }

                public SaveSlot()
                {
                        // Parameterless constructor for deserialization
                        CustomMetadata = new Dictionary<string, string>();
                }
	}
}
#endif