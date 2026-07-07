// Veldrath Desktop Bridge — JavaScript interop for native features
// Injected into the Blazor game UI when running inside the Avalonia desktop shell.
// Communicates with NativeBridgeService via window.chrome.webview.postMessage.

(function () {
    'use strict';

    // Guard: only define the bridge when running inside a WebView2 control.
    if (typeof window.chrome === 'undefined' || !window.chrome.webview) {
        console.log('[VeldrathBridge] Not running inside WebView2 — bridge disabled.');
        return;
    }

    console.log('[VeldrathBridge] Initializing...');

    // ── Bridge API ──────────────────────────────────────────────────────────────

    window.VeldrathBridge = {
        /**
         * Plays a music track (background loop) from the given file path.
         * @param {string} url - Absolute file path or URL to the audio file.
         */
        playAudio: function (url) {
            window.chrome.webview.postMessage(JSON.stringify({
                type: 'audio:play',
                data: { url: url }
            }));
        },

        /**
         * Stops the currently playing music track.
         */
        stopAudio: function () {
            window.chrome.webview.postMessage(JSON.stringify({
                type: 'audio:stop'
            }));
        },

        /**
         * Sets the background music volume.
         * @param {number} volume - Volume level from 0 (silent) to 100 (full).
         */
        setMusicVolume: function (volume) {
            window.chrome.webview.postMessage(JSON.stringify({
                type: 'audio:setMusicVolume',
                data: { volume: volume }
            }));
        },

        /**
         * Sets the sound effects volume.
         * @param {number} volume - Volume level from 0 (silent) to 100 (full).
         */
        setSfxVolume: function (volume) {
            window.chrome.webview.postMessage(JSON.stringify({
                type: 'audio:setSfxVolume',
                data: { volume: volume }
            }));
        },

        /**
         * Mutes or unmutes all audio output.
         * @param {boolean} muted - true to mute, false to unmute.
         */
        setMuted: function (muted) {
            window.chrome.webview.postMessage(JSON.stringify({
                type: 'audio:setMuted',
                data: { muted: muted }
            }));
        },

        /**
         * Shows a desktop notification.
         * @param {string} title - Notification title.
         * @param {string} message - Notification body text.
         */
        showNotification: function (title, message) {
            window.chrome.webview.postMessage(JSON.stringify({
                type: 'notification:show',
                data: { title: title, message: message }
            }));
        },

        /**
         * Copies the given text to the system clipboard.
         * @param {string} text - The text to copy.
         */
        copyToClipboard: function (text) {
            window.chrome.webview.postMessage(JSON.stringify({
                type: 'clipboard:copy',
                data: { text: text }
            }));
        }
    };

    // ── Initialization ──────────────────────────────────────────────────────────

    // Signal to the native host that the bridge is ready.
    window.chrome.webview.postMessage(JSON.stringify({
        type: 'bridge:ready'
    }));

    console.log('[VeldrathBridge] Bridge initialized successfully.');
})();

// ── Browser tab/window close protection ─────────────────────────────────────
// Works in all browsers (not just WebView2).
// Used by the character creation wizard to warn users before navigating away
// with unsaved progress.

window.characterCreation = {
    enableBeforeUnload: function () {
        window.addEventListener('beforeunload', window.characterCreation._onBeforeUnload);
    },
    disableBeforeUnload: function () {
        window.removeEventListener('beforeunload', window.characterCreation._onBeforeUnload);
    },
    _onBeforeUnload: function (e) {
        e.preventDefault();
        e.returnValue = 'You have an unfinished character. Are you sure you want to leave?';
    }
};
