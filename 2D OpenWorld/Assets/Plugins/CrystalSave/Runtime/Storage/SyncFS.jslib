mergeInto(LibraryManager.library, {
    // Flushes pending writes from MEMFS to IDBFS, with guards to avoid
    // throwing in environments where IndexedDB is unavailable or IDBFS
    // hasn't been mounted yet.
    SyncFS: function() {
        try {
            if (typeof indexedDB === 'undefined') return;
            if (typeof FS === 'undefined' || typeof IDBFS === 'undefined') return;
            // Ensure mount exists
            try { FS.lookupPath('/idbfs'); }
            catch (e) {
                try { FS.mkdir('/idbfs'); } catch (e2) {}
                try { FS.mount(IDBFS, {}, '/idbfs'); } catch (e3) {}
            }
            // If not initialised yet, perform a populate pass first
            var m = (typeof Module !== 'undefined') ? Module : {};
            if (!m.__CrystalSaveFSReady && !m.__CrystalSaveFSBusy) {
                m.__CrystalSaveFSBusy = 1;
                FS.syncfs(true, function(err) {
                    if (err) console.error('SyncFS (populate) failed', err);
                    try { if (typeof Module !== 'undefined') Module.__CrystalSaveFSReady = 1; } catch (e) {}
                    try { if (typeof Module !== 'undefined') Module.__CrystalSaveFSBusy = 0; } catch (e) {}
                });
                return;
            }
            // Cross-file busy flag to avoid concurrent flushes
            if (m.__CrystalSaveFSBusy) return;
            m.__CrystalSaveFSBusy = 1;
            FS.syncfs(false, function(err) {
                if (err) console.error('SyncFS failed', err);
                try { if (typeof Module !== 'undefined') Module.__CrystalSaveFSBusy = 0; } catch (e) {}
            });
        } catch (ex) {
            console.error('SyncFS exception', ex);
        }
    }
});
