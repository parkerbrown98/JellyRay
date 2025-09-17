(function () {
    console.log("[JellyRay] Script loaded");

    let lastHash = "";

    function checkPage() {
        const hash = window.location.hash;

        if (hash.startsWith("#/video")) {
            if (lastHash !== hash) {
                console.log("[JellyRay] Entered video page:", hash);
                onVideoPageLoad();
                lastHash = hash;
            }
        } else {
            lastHash = hash;
        }

        setTimeout(checkPage, 1000); // poll every second
    }

    function getDeviceId() {
        return ApiClient.deviceId();
    }

    async function getContentItemId() {
        const device = getDeviceId();
        const sessions = await ApiClient.getSessions();
        const ourSession = sessions.find(s => s.DeviceId === device);
        return ourSession?.NowPlayingItem?.Id;
    }

    function onVideoPageLoad() {
        const video = document.querySelector("video");

        if (!video) {
            console.log("[JellyRay] Waiting for <video> element...");
            setTimeout(onVideoPageLoad, 500);
            return;
        }

        console.log("[JellyRay] Hooking <video> element");

        video.addEventListener("pause", async () => {
            console.log("[JellyRay] Pause detected at", video.currentTime);

            const itemId = await getContentItemId();
            const ticks = Math.floor(video.currentTime * 10_000_000); // seconds → ticks

            if (!itemId) {
                console.warn("[JellyRay] Could not resolve ItemId");
                return;
            }

            try {
                const res = await fetch(`/JellyRay/faces?itemId=${itemId}&ticks=${ticks}`);
                const data = await res.json();
                console.log("[JellyRay] Faces:", data);

                showOverlay(data.faces);
            } catch (err) {
                console.error("[JellyRay] Error fetching faces", err);
            }
        });

        video.addEventListener("play", () => {
            console.log("[JellyRay] Play detected, removing overlay");
            removeOverlay();
        });
    }

    function showOverlay(faces) {
        removeOverlay();

        const overlay = document.createElement("div");
        overlay.id = "jellyray-overlay";
        overlay.style.position = "absolute";
        overlay.style.bottom = "10%";
        overlay.style.left = "10%";
        overlay.style.padding = "1em";
        overlay.style.background = "rgba(0,0,0,0.75)";
        overlay.style.color = "white";
        overlay.style.borderRadius = "8px";
        overlay.style.zIndex = "9999";

        if (!faces || faces.length === 0) {
            overlay.textContent = "No celebrities detected.";
        } else {
            overlay.innerHTML = faces
                .map((f) => `${f.name} (${Math.round(f.confidence * 100)}%)`)
                .join("<br>");
        }

        document.body.appendChild(overlay);
    }

    function removeOverlay() {
        const existing = document.getElementById("jellyray-overlay");
        if (existing) existing.remove();
    }

    checkPage(); // start polling
})();
