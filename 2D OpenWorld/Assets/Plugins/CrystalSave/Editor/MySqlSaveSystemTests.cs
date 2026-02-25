#if ARAWN_REMEMBERME && MEMORYPACK
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.EditorTests
{
    public class MySqlSaveSystemTests
    {
        [Test]
        public async Task SaveLoadDeleteCycle()
        {
            using var listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:5005/");
            listener.Start();

            var serverTask = Task.Run(async () =>
            {
                var data = new Dictionary<int, byte[]>();
                var meta = new Dictionary<int, SlotMeta>();
                while (listener.IsListening)
                {
                    var ctx = await listener.GetContextAsync();
                    string action = ctx.Request.Url.AbsolutePath.Trim('/');
                    using var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
                    string body = await reader.ReadToEndAsync();
                    switch (action)
                    {
                        case "save":
                            var saveReq = JsonUtility.FromJson<SaveReq>(body);
                            data[saveReq.slot] = Convert.FromBase64String(saveReq.data);
                            break;
                        case "load":
                            int lslot = int.Parse(ctx.Request.QueryString["slot"]);
                            if (data.TryGetValue(lslot, out var d))
                            {
                                byte[] buf = Encoding.UTF8.GetBytes(Convert.ToBase64String(d));
                                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                            }
                            break;
                        case "delete":
                            var delReq = JsonUtility.FromJson<DeleteReq>(body);
                            data.Remove(delReq.slot);
                            meta.Remove(delReq.slot);
                            break;
                        case "metadata":
                            var mReq = JsonUtility.FromJson<MetaReq>(body);
                            meta[mReq.slot] = new SlotMeta
                            {
                                slot = mReq.slot,
                                name = mReq.name,
                                ticks = mReq.ticks,
                                scene = mReq.scene,
                                shot = mReq.shot,
                                meta = mReq.meta
                            };
                            break;
                        case "list":
                            var sb = new StringBuilder();
                            sb.Append('[');
                            bool first = true;
                            foreach (var m in meta.Values)
                            {
                                if (!first) sb.Append(',');
                                sb.Append(JsonUtility.ToJson(m));
                                first = false;
                            }
                            sb.Append(']');
                            byte[] outBuf = Encoding.UTF8.GetBytes(sb.ToString());
                            ctx.Response.OutputStream.Write(outBuf, 0, outBuf.Length);
                            break;
                    }
                    ctx.Response.OutputStream.Close();
                }
            });

            var settings = ScriptableObject.CreateInstance<SaveSettings>();
            settings.backend        = SaveBackend.MySQL;
            settings.mySqlApiUrl    = "http://localhost:5005";
            settings.tableName      = "CrystalSaveData";
            settings.keepLocalMirror = false;
            var system = new MySqlSaveSystem(settings, Application.persistentDataPath);

            var slot = new SaveSlot(1, "Test", DateTime.UtcNow, string.Empty, "Scene");
            slot.CustomMetadata["Player"] = "Alice";
            byte[] bytes = new byte[] {1,2,3};
            await system.SaveAsync(bytes, slot);
            var metaSlot = await system.LoadSlotMetadataAsync(slot.SlotNumber);
            Assert.AreEqual("Alice", metaSlot.CustomMetadata["Player"]);
            var list = await system.ListRemoteSlotsAsync();
            Assert.AreEqual("Alice", list[0].CustomMetadata["Player"]);
            var loaded = await system.LoadAsync(slot);
            Assert.AreEqual(bytes, loaded);
            await system.DeleteAsync(slot);
            var afterDelete = await system.LoadAsync(slot);
            Assert.IsNull(afterDelete);

            listener.Stop();
        }

        [Serializable] class MetaPair { public string key; public string value; }
        [Serializable] class SaveReq { public string uid; public int slot; public string table; public string data; }
        [Serializable] class DeleteReq { public string uid; public int slot; public string table; }
        [Serializable] class MetaReq { public string uid; public int slot; public string table; public string name; public long ticks; public string scene; public string shot; public List<MetaPair> meta; }
        [Serializable] class SlotMeta { public int slot; public string name; public long ticks; public string scene; public string shot; public List<MetaPair> meta; }
        [Serializable] class SlotMetaList { public SlotMeta[] items; }
    }
}
#endif
