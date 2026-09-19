(() => {
    window.onerror = function (message, source, lineno, colno, error) {
        window.chrome?.webview?.postMessage({
            type: "classroom-event",
            event: "error",
            ts: Date.now(),
            reason: `JS Error: ${message} at ${source}:${lineno}:${colno}`
        });
    };
    window.onunhandledrejection = function (event) {
        window.chrome?.webview?.postMessage({
            type: "classroom-event",
            event: "error",
            ts: Date.now(),
            reason: `JS Promise Rejection: ${event.reason?.message || event.reason}`
        });
    };

    const MAX_STUDENTS = 5;
    const TEACHER_ROLE = "teacher";
    const STUDENT_ROLE = "student";
    const canvas = document.getElementById("mediaCanvas");
    const mainStage = document.getElementById("mainStage");
    const filmstrip = document.getElementById("filmstrip");

    const state = {
        config: null,
        signaling: null,
        started: false,
        joinedAt: new Date().toISOString(),
        localStream: null,
        localCameraTrack: null,
        localAudioTrack: null,
        screenTrack: null,
        screenStream: null,
        micEnabled: true,
        cameraEnabled: true,
        screenSharing: false,
        peers: new Map(),
        participants: new Map(),
        makingOffers: new Set(),
        pendingIceCandidates: new Map(),
        announcedPeerIds: new Set(),
        presenceTimers: [],
        currentScreenShareOwnerId: null,
        pendingScreenShareOwnerId: null,
        tileCache: new Map()
    };

    function post(event, payload = {}) {
        window.chrome?.webview?.postMessage({
            type: "classroom-event",
            event,
            ts: Date.now(),
            ...payload
        });
    }

    function postState(stateName, reason = null) {
        window.chrome?.webview?.postMessage({
            type: "webrtc-state",
            state: stateName,
            reason
        });
    }

    function reportError(error, fallbackMessage) {
        const message = error?.message || fallbackMessage || "Lỗi WebRTC không xác định.";
        post("error", { reason: message });
        postState("failed", message);
    }

    function parseStunUrls(value) {
        if (Array.isArray(value)) {
            return value.filter(Boolean);
        }

        return String(value || "stun:stun.l.google.com:19302")
            .split(",")
            .map((item) => item.trim())
            .filter(Boolean);
    }

    function parseIceServers(config) {
        if (Array.isArray(config.iceServers) && config.iceServers.length > 0) {
            return config.iceServers
                .filter((server) => server && server.urls)
                .map((server) => ({
                    urls: Array.isArray(server.urls) ? server.urls : String(server.urls).split(",").map((item) => item.trim()).filter(Boolean),
                    username: server.username || undefined,
                    credential: server.credential || undefined
                }));
        }

        return parseStunUrls(config.stunUrls).map((url) => ({ urls: url }));
    }

    function getConfig() {
        const config = window.__COURSEGUARD_WEBRTC_CONFIG__;
        if (!config) {
            throw new Error("WebRTC config chưa được C# inject vào WebView2.");
        }

        const required = ["sessionId", "roomId", "userId", "role"];
        for (const key of required) {
            if (config[key] === undefined || config[key] === null || config[key] === "") {
                throw new Error(`Thiếu cấu hình WebRTC: ${key}`);
            }
        }

        if (!config.relayWsUrl && (!config.supabaseUrl || !config.supabaseAnonKey)) {
            throw new Error("Thiếu cấu hình WebRTC signaling: cần RELAY_WS_URL hoặc SUPABASE_URL/SUPABASE_ANON_KEY.");
        }

        return {
            ...config,
            userId: String(config.userId),
            role: String(config.role).toLowerCase(),
            displayName: config.displayName || config.role,
            stunUrls: parseStunUrls(config.stunUrls),
            iceServers: parseIceServers(config)
        };
    }

    function isTeacher() {
        return state.config?.role === TEACHER_ROLE;
    }

    function isStudent() {
        return state.config?.role === STUDENT_ROLE;
    }

    function makePeerKey(peerId) {
        return String(peerId);
    }

    function initials(name) {
        const value = String(name || "CG").trim();
        if (!value) return "CG";
        const parts = value.split(/\s+/).slice(0, 2);
        return parts.map((part) => part[0]?.toUpperCase() || "").join("") || "CG";
    }

    function syncRemoteScreenStream(ownerId = state.currentScreenShareOwnerId) {
        if (!ownerId) {
            state.screenStream = null;
            return false;
        }

        const owner = state.participants.get(makePeerKey(ownerId));
        state.screenStream = owner?.screenStream || null;
        return !!state.screenStream;
    }

    function isRemoteScreenOwner(peerId) {
        return !!state.currentScreenShareOwnerId && makePeerKey(peerId) === makePeerKey(state.currentScreenShareOwnerId);
    }

    function createTile(id, displayName, stream, options = {}) {
        const key = makePeerKey(id);
        let tile = state.tileCache.get(key);
        let video;
        let placeholder;

        if (!tile) {
            tile = document.createElement("article");
            tile.className = "video-tile";
            tile.dataset.peerId = key;

            video = document.createElement("video");
            video.autoplay = true;
            video.playsInline = true;
            video.disablePictureInPicture = true;

            placeholder = document.createElement("div");
            placeholder.className = "video-placeholder";

            const avatarCircle = document.createElement("div");
            avatarCircle.className = "avatar-circle";

            const avatarName = document.createElement("div");
            avatarName.className = "avatar-name";

            placeholder.append(avatarCircle, avatarName);
            tile.append(video, placeholder);
            state.tileCache.set(key, tile);
        } else {
            video = tile.querySelector("video");
            placeholder = tile.querySelector(".video-placeholder");
        }

        tile.dataset.peerId = key;
        tile.classList.toggle("screen", !!options.screen);
        tile.classList.toggle("muted", !!options.muted);
        tile.classList.toggle("focused", !!options.focused);
        tile.classList.toggle("camera-off", !!options.cameraOff);
        tile.classList.toggle("strip-tile", !!options.strip);
        video.muted = !!options.muted;

        if (video.srcObject !== (stream || null)) {
            video.srcObject = stream || null;
        }

        const avatarCircle = placeholder.querySelector(".avatar-circle");
        const avatarName = placeholder.querySelector(".avatar-name");
        if (avatarCircle) avatarCircle.textContent = initials(displayName);
        if (avatarName) avatarName.textContent = displayName || "CourseGuard";
        return tile;
    }

    function removeTile(peerId) {
        const key = makePeerKey(peerId);
        const tile = state.tileCache.get(key);
        if (tile) {
            const video = tile.querySelector("video");
            if (video) video.srcObject = null;
            tile.remove();
            state.tileCache.delete(key);
        }
    }

    function moveTile(parent, tile) {
        if (tile.parentElement !== parent) {
            parent.appendChild(tile);
        }
    }

    function detachUnusedTiles(activeKeys) {
        for (const [key, tile] of state.tileCache.entries()) {
            if (!activeKeys.has(key) && tile.parentElement) {
                tile.remove();
            }
        }
    }

    function renderLayout() {
        if (!canvas || !mainStage || !filmstrip || !state.config) return;

        const activeKeys = new Set();
        const localName = state.config.displayName || "Bạn";
        const localKey = makePeerKey(state.config.userId || "local");
        const localTile = createTile(localKey, localName, state.localStream, {
            muted: true,
            cameraOff: !state.cameraEnabled,
            strip: state.screenSharing && !!state.screenStream
        });
        activeKeys.add(localKey);

        const remoteTiles = [];
        for (const participant of state.participants.values()) {
            const participantKey = makePeerKey(participant.id);
            if (state.screenSharing && state.screenStream && isRemoteScreenOwner(participantKey)) {
                continue;
            }

            const tile = createTile(participantKey, participant.displayName, participant.cameraStream || participant.stream || null, {
                cameraOff: participant.cameraEnabled === false,
                strip: state.screenSharing && !!state.screenStream
            });
            activeKeys.add(participantKey);
            remoteTiles.push(tile);
        }

        const hasScreenShare = state.screenSharing && !!state.screenStream;
        canvas.classList.toggle("screen-share", hasScreenShare);
        canvas.classList.toggle("has-filmstrip", !hasScreenShare && remoteTiles.length > 0);
        canvas.classList.toggle("screen-share-mode", hasScreenShare);

        if (hasScreenShare) {
            const owner = state.participants.get(makePeerKey(state.currentScreenShareOwnerId || ""));
            const screenTitle = owner?.displayName ? `Màn hình ${owner.displayName}` : "Màn hình giáo viên";
            const screenTile = createTile("teacher-screen", screenTitle, state.screenStream, {
                screen: true,
                focused: true,
                cameraOff: false
            });
            activeKeys.add("teacher-screen");
            moveTile(mainStage, screenTile);
            moveTile(filmstrip, localTile);
            for (const tile of remoteTiles.slice(0, 5)) {
                moveTile(filmstrip, tile);
            }
            detachUnusedTiles(activeKeys);
            return;
        }

        moveTile(mainStage, localTile);
        for (const tile of remoteTiles) {
            moveTile(mainStage, tile);
        }
        detachUnusedTiles(activeKeys);
    }

    async function start() {
        if (state.started) return;

        try {
            state.config = getConfig();
            state.joinedAt = new Date().toISOString();
            postState("requesting-device");
            await startLocalMedia();
            postState("media-ready");

            if (state.config.relayWsUrl && window.CourseGuardRelaySignaling) {
                state.signaling = new window.CourseGuardRelaySignaling(state.config, (message) => {
                    post("log", { message });
                }, { joinedAt: state.joinedAt });
            } else {
                state.signaling = new window.CourseGuardSupabaseTableSignaling(state.config, (message) => {
                    post("log", { message });
                }, { joinedAt: state.joinedAt });
            }
            state.signaling.onSignal(handleSignal);
            await state.signaling.start();

            state.started = true;
            announcePresence();
            postState("started");
            post("started", { userId: state.config.userId, role: state.config.role });
            renderLayout();
        } catch (error) {
            reportError(error, "Không thể khởi tạo WebRTC.");
            await cleanup(false, false);
        }
    }

    async function startLocalMedia() {
        if (!navigator.mediaDevices?.getUserMedia) {
            throw new Error("WebView2 hiện tại không hỗ trợ getUserMedia. Hãy cập nhật Microsoft Edge WebView2 Runtime.");
        }

        const stream = new MediaStream();
        const audioTrack = await requestTrack("audio", {
            audio: {
                echoCancellation: true,
                noiseSuppression: true,
                autoGainControl: true
            },
            video: false
        });
        const videoTrack = await requestTrack("video", {
            audio: false,
            video: {
                width: { ideal: 1280 },
                height: { ideal: 720 },
                frameRate: { ideal: 24, max: 30 }
            }
        });

        if (audioTrack) stream.addTrack(audioTrack);
        if (videoTrack) stream.addTrack(videoTrack);
        if (stream.getTracks().length === 0) {
            post("media-warning", { kind: "system", reason: "Không tìm thấy thiết bị thu âm/hình khả dụng. Kết nối chế độ chỉ nghe/xem." });
        }

        state.localStream = stream;
        state.localAudioTrack = audioTrack;
        state.localCameraTrack = videoTrack;
        state.micEnabled = !!audioTrack;
        state.cameraEnabled = !!videoTrack;
        renderLayout();
    }

    async function requestTrack(kind, constraints) {
        try {
            const stream = await navigator.mediaDevices.getUserMedia(constraints);
            const selected = kind === "video" ? stream.getVideoTracks()[0] : stream.getAudioTracks()[0];
            for (const track of stream.getTracks()) {
                if (track !== selected) track.stop();
            }
            return selected || null;
        } catch (error) {
            post("media-warning", { kind, reason: error?.message || String(error) });
            return null;
        }
    }

    function compactParticipantPayload() {
        return {
            n: state.config.displayName,
            r: state.config.role,
            c: state.cameraEnabled ? 1 : 0,
            m: state.micEnabled ? 1 : 0,
            t: state.joinedAt
        };
    }

    function announcePresence() {
        const delays = [0, 700, 1800, 3500];
        for (const delay of delays) {
            const timer = window.setTimeout(() => {
                void state.signaling?.send("participant-join", compactParticipantPayload());
            }, delay);
            state.presenceTimers.push(timer);
        }
    }

    function normalizeParticipant(row) {
        const payload = row.payload || {};
        return {
            id: String(row.sender_id),
            role: String(row.sender_role || payload.r || "").toLowerCase(),
            displayName: payload.n || payload.displayName || row.sender_name || row.sender_id,
            cameraEnabled: payload.c === undefined ? payload.cameraEnabled !== false : payload.c === 1,
            micEnabled: payload.m === undefined ? payload.micEnabled !== false : payload.m === 1,
            cameraStream: null,
            screenStream: null,
            stream: null
        };
    }

    function createPeerConnection(peerId) {
        const key = makePeerKey(peerId);
        const existing = state.peers.get(key);
        if (existing) return existing;

        const peer = new RTCPeerConnection({
            iceServers: state.config.iceServers
        });

        state.peers.set(key, peer);

        const tracks = state.localStream?.getTracks() || [];
        for (const track of tracks) {
            peer.addTrack(track, state.localStream);
        }

        if (isTeacher() && state.screenSharing && state.screenTrack && state.screenStream) {
            peer._courseGuardScreenSender = peer.addTrack(state.screenTrack, state.screenStream);
        }

        peer.onicecandidate = (event) => {
            if (event.candidate) {
                void state.signaling?.send("ice-candidate", minimizeIceCandidate(event.candidate), key);
            }
        };

        peer.onnegotiationneeded = async () => {
            if (!isTeacher()) {
                return;
            }

            try {
                await makeOffer(key);
            } catch (err) {
                post("log", { message: `Renegotiation error for ${key}: ${err.message}` });
            }
        };

        peer.ontrack = (event) => {
            const stream = event.streams[0] || new MediaStream([event.track]);
            const participant = state.participants.get(key) || {
                id: key,
                role: "remote",
                displayName: key,
                cameraEnabled: true,
                micEnabled: true
            };
            const isScreenTrack = event.track.kind === "video" && isScreenTrackEvent(key, participant);
            if (isScreenTrack) {
                participant.screenStream = stream;
                if (isRemoteScreenOwner(key) || state.pendingScreenShareOwnerId === key) {
                    state.screenStream = stream;
                    state.currentScreenShareOwnerId = key;
                    state.pendingScreenShareOwnerId = null;
                    post("screen-share-track-ready", { userId: key });
                    postState("screen-share-track-ready");
                }
            } else {
                participant.cameraStream = stream;
                participant.stream = stream;
            }
            state.participants.set(key, participant);

            post("peer-connected", { userId: key, displayName: participant.displayName });
            postState("connected");
            renderLayout();
        };

        peer.onconnectionstatechange = () => {
            const connectionState = peer.connectionState;
            post("peer-state", { userId: key, state: connectionState });
            if (connectionState === "failed" || connectionState === "closed" || connectionState === "disconnected") {
                if (connectionState !== "disconnected") {
                    closePeer(key, true);
                }
            }
        };

        return peer;
    }

    function isScreenTrackEvent(peerId, participant) {
        if (!state.screenSharing && !state.pendingScreenShareOwnerId && !state.currentScreenShareOwnerId) {
            return false;
        }

        if (state.pendingScreenShareOwnerId === peerId || isRemoteScreenOwner(peerId)) {
            return !!participant?.cameraStream;
        }

        return false;
    }

    function minimizeIceCandidate(candidate) {
        const json = candidate.toJSON();
        return {
            candidate: json.candidate,
            sdpMid: json.sdpMid,
            sdpMLineIndex: json.sdpMLineIndex
        };
    }

    function upsertRemoteParticipant(row) {
        const participant = normalizeParticipant(row);
        const existing = state.participants.get(participant.id) || {};
        state.participants.set(participant.id, {
            ...existing,
            ...participant,
            cameraStream: existing.cameraStream || existing.stream || null,
            screenStream: existing.screenStream || null,
            stream: existing.stream || null
        });
        renderLayout();
        return participant;
    }

    async function addIceCandidateOrQueue(peerId, candidatePayload) {
        const key = makePeerKey(peerId);
        const peer = createPeerConnection(key);
        if (!peer.remoteDescription) {
            const queue = state.pendingIceCandidates.get(key) || [];
            queue.push(candidatePayload);
            state.pendingIceCandidates.set(key, queue);
            return;
        }

        await peer.addIceCandidate(new RTCIceCandidate(candidatePayload));
    }

    async function flushPendingIceCandidates(peerId) {
        const key = makePeerKey(peerId);
        const peer = state.peers.get(key);
        const queue = state.pendingIceCandidates.get(key);
        if (!peer || !peer.remoteDescription || !queue?.length) {
            return;
        }

        state.pendingIceCandidates.delete(key);
        for (const candidatePayload of queue) {
            await peer.addIceCandidate(new RTCIceCandidate(candidatePayload));
        }
    }

    async function makeOffer(peerId) {
        const key = makePeerKey(peerId);
        if (state.makingOffers.has(key)) return;
        state.makingOffers.add(key);

        try {
            const peer = createPeerConnection(key);
            const offer = await peer.createOffer();
            await peer.setLocalDescription(offer);
            await state.signaling?.send("offer", {
                type: peer.localDescription.type,
                sdp: peer.localDescription.sdp
            }, key);
        } finally {
            state.makingOffers.delete(key);
        }
    }

    function shouldSendTeacherOffer(participantId) {
        if (!isTeacher()) {
            return false;
        }

        const peer = state.peers.get(makePeerKey(participantId));
        if (!peer) {
            return true;
        }

        return peer.connectionState !== "connected"
            && peer.connectionState !== "completed";
    }

    async function handleSignal(row) {
        try {
            const type = row.signal_type;
            const senderId = String(row.sender_id);
            if (!state.config || senderId === state.config.userId) return;

            if (type === "participant-join") {
                await handleParticipantJoin(row);
                return;
            }

            if (type === "participant-leave") {
                closePeer(senderId, true);
                post("participant-left", { userId: senderId });
                return;
            }

            if (type === "media-state") {
                updateParticipantMedia(senderId, row.payload || {});
                return;
            }

            if (type === "screen-share-state") {
                state.screenSharing = !!row.payload?.sharing;
                state.currentScreenShareOwnerId = state.screenSharing ? senderId : null;
                if (state.screenSharing) {
                    state.pendingScreenShareOwnerId = senderId;
                    const hasScreenStream = syncRemoteScreenStream(senderId);
                    post("screen-share-loading", { userId: senderId });
                    postState("screen-share-loading");
                    if (hasScreenStream) {
                        state.pendingScreenShareOwnerId = null;
                        post("screen-share-track-ready", { userId: senderId });
                        postState("screen-share-track-ready");
                    }
                } else {
                    state.pendingScreenShareOwnerId = null;
                    state.screenStream = null;
                }
                post(state.screenSharing ? "screen-share-started" : "screen-share-stopped", { userId: senderId });
                renderLayout();
                return;
            }

            if (type === "teacher-mute" && isStudent()) {
                await setMicEnabled(false, true);
                post("teacher-muted-you", { teacherId: senderId });
                return;
            }

            if (type === "teacher-kick" && isStudent()) {
                post("kicked", { teacherId: senderId });
                await cleanup(true);
                return;
            }

            if (type === "room-full") {
                post("room-full", { reason: "Lớp học đã đủ số lượng tối đa" });
                postState("room-full", "Lớp học đã đủ số lượng tối đa");
                await cleanup(false, false);
                return;
            }

            if (type === "offer") {
                upsertRemoteParticipant(row);
                const peer = createPeerConnection(senderId);
                const isPolite = isStudent();
                const offerCollision = state.makingOffers.has(makePeerKey(senderId)) || peer.signalingState !== "stable";

                if (offerCollision && !isPolite) {
                    post("log", { message: `Ignoring colliding offer from ${senderId} (impolite peer)` });
                    return;
                }

                if (offerCollision && isPolite) {
                    await peer.setLocalDescription({ type: "rollback" });
                }

                await peer.setRemoteDescription(new RTCSessionDescription(row.payload));
                await flushPendingIceCandidates(senderId);
                const answer = await peer.createAnswer();
                await peer.setLocalDescription(answer);
                await state.signaling?.send("answer", {
                    type: peer.localDescription.type,
                    sdp: peer.localDescription.sdp
                }, senderId);
                return;
            }

            if (type === "answer") {
                upsertRemoteParticipant(row);
                const peer = state.peers.get(senderId);
                if (peer && peer.signalingState !== "stable") {
                    await peer.setRemoteDescription(new RTCSessionDescription(row.payload));
                    await flushPendingIceCandidates(senderId);
                }
                return;
            }

            if (type === "ice-candidate") {
                upsertRemoteParticipant(row);
                await addIceCandidateOrQueue(senderId, row.payload);
            }
        } catch (error) {
            reportError(error, "Lỗi xử lý tín hiệu WebRTC.");
        }
    }

    async function handleParticipantJoin(row) {
        const participant = normalizeParticipant(row);
        const currentStudents = [...state.participants.values()].filter((item) => item.role === STUDENT_ROLE).length;

        if (isTeacher() && participant.role === STUDENT_ROLE && !state.participants.has(participant.id) && currentStudents >= MAX_STUDENTS) {
            await state.signaling?.send("room-full", { reason: "full" }, participant.id);
            post("room-full-sent", { userId: participant.id });
            return;
        }

        const isNewParticipant = !state.participants.has(participant.id);
        upsertRemoteParticipant(row);
        post("participant-joined", {
            userId: participant.id,
            role: participant.role,
            displayName: participant.displayName
        });
        renderLayout();

        const shouldOffer = isTeacher() && participant.role === STUDENT_ROLE;

        if (shouldOffer && shouldSendTeacherOffer(participant.id)) {
            await makeOffer(participant.id);
        }

        if (isNewParticipant) {
            await state.signaling?.send("participant-join", compactParticipantPayload());
        }
    }

    function updateParticipantMedia(peerId, payload) {
        const key = makePeerKey(peerId);
        const participant = state.participants.get(key);
        if (!participant) return;

        if (payload.c !== undefined) participant.cameraEnabled = payload.c === 1 || payload.c === true;
        if (payload.m !== undefined) participant.micEnabled = payload.m === 1 || payload.m === true;
        post("participant-media-state", {
            userId: key,
            cameraEnabled: participant.cameraEnabled,
            micEnabled: participant.micEnabled
        });
        renderLayout();
    }

    async function setMicEnabled(enabled, fromTeacher = false) {
        state.micEnabled = !!enabled;
        for (const track of state.localStream?.getAudioTracks() || []) {
            track.enabled = state.micEnabled;
        }
        post("mic-state", { enabled: state.micEnabled, fromTeacher });
        await state.signaling?.send("media-state", { m: state.micEnabled ? 1 : 0, c: state.cameraEnabled ? 1 : 0 });
    }

    async function setCameraEnabled(enabled) {
        state.cameraEnabled = !!enabled;
        for (const track of state.localStream?.getVideoTracks() || []) {
            track.enabled = state.cameraEnabled;
        }
        post("camera-state", { enabled: state.cameraEnabled });
        renderLayout();
        await state.signaling?.send("media-state", { m: state.micEnabled ? 1 : 0, c: state.cameraEnabled ? 1 : 0 });
    }

    async function startScreenShare() {
        if (!isTeacher()) {
            post("screen-share-denied", { reason: "Chỉ giáo viên được chia sẻ màn hình." });
            return;
        }

        if (!state.started) await start();
        if (state.screenSharing) return;
        if (!navigator.mediaDevices?.getDisplayMedia) {
            throw new Error("WebView2 hiện tại không hỗ trợ chia sẻ màn hình.");
        }

        try {
            const displayStream = await navigator.mediaDevices.getDisplayMedia({
                video: {
                    frameRate: { ideal: 15, max: 24 },
                    width: { ideal: 1920 },
                    height: { ideal: 1080 }
                },
                audio: false
            });
            const screenTrack = displayStream.getVideoTracks()[0];
            if (!screenTrack) throw new Error("Không lấy được luồng màn hình để trình bày.");

            state.screenStream = displayStream;
            state.screenTrack = screenTrack;
            state.screenSharing = true;
            state.currentScreenShareOwnerId = state.config.userId;

            post("screen-share-connecting");
            postState("screen-share-connecting");

            const replaceTasks = [];
            for (const peer of state.peers.values()) {
                const sender = peer._courseGuardScreenSender;
                if (sender) {
                    replaceTasks.push(sender.replaceTrack(screenTrack));
                } else {
                    peer._courseGuardScreenSender = peer.addTrack(screenTrack, displayStream);
                }
            }
            const results = await Promise.allSettled(replaceTasks);
            const failed = results.find((item) => item.status === "rejected");
            if (failed) throw failed.reason;

            screenTrack.onended = () => void stopScreenShare();
            await state.signaling?.send("screen-share-state", { sharing: true, ownerId: state.config.userId });
            post("screen-share-started");
            post("screen-share-track-ready", { userId: state.config.userId });
            postState("screen-share-started");
            renderLayout();
        } catch (error) {
            state.screenSharing = false;
            state.currentScreenShareOwnerId = null;
            state.screenTrack = null;
            state.screenStream = null;
            reportError(error, "Không thể trình bày màn hình qua WebRTC.");
            postState("screen-share-failed", error?.message || null);
        }
    }

    async function stopScreenShare() {
        if (!state.screenSharing && !state.screenTrack) return;

        const oldTrack = state.screenTrack;
        const oldStream = state.screenStream;
        state.screenSharing = false;
        state.currentScreenShareOwnerId = null;
        state.pendingScreenShareOwnerId = null;
        state.screenTrack = null;
        state.screenStream = null;

        if (oldTrack) {
            oldTrack.onended = null;
            oldTrack.stop();
        }
        for (const track of oldStream?.getTracks() || []) {
            if (track !== oldTrack) track.stop();
        }

        const replaceTasks = [];
        for (const peer of state.peers.values()) {
            const sender = peer._courseGuardScreenSender;
            if (sender) replaceTasks.push(sender.replaceTrack(null));
        }
        await Promise.allSettled(replaceTasks);

        await state.signaling?.send("screen-share-state", { sharing: false });
        post("screen-share-stopped");
        postState("screen-share-stopped");
        renderLayout();
    }

    async function toggleScreenShare() {
        if (state.screenSharing) {
            await stopScreenShare();
        } else {
            await startScreenShare();
        }
    }

    function closePeer(peerId, removeParticipant) {
        const key = makePeerKey(peerId);
        const peer = state.peers.get(key);
        if (peer) {
            peer.onicecandidate = null;
            peer.ontrack = null;
            peer.onconnectionstatechange = null;
            peer.close();
            state.peers.delete(key);
        }
        state.pendingIceCandidates.delete(key);
        if (removeParticipant) {
            state.participants.delete(key);
        } else {
            const participant = state.participants.get(key);
            if (participant) {
                participant.stream = null;
                participant.cameraStream = null;
                participant.screenStream = null;
            }
        }
        removeTile(key);
        renderLayout();
    }

    async function cleanup(notify = true, markLeft = true) {
        try {
            if (notify && state.signaling && state.config) {
                await state.signaling.send("participant-leave", { n: state.config.displayName });
            }
        } catch {
            // Best-effort leave notification.
        }

        try {
            await stopScreenShare();
        } catch {
            // Continue cleanup even if replacing tracks fails.
        }

        for (const key of [...state.peers.keys()]) {
            closePeer(key, true);
        }

        for (const track of state.localStream?.getTracks() || []) {
            track.stop();
        }
        state.localStream = null;
        state.localCameraTrack = null;
        state.localAudioTrack = null;
        state.screenTrack = null;
        state.screenStream = null;
        state.participants.clear();
        state.peers.clear();
        state.announcedPeerIds.clear();
        for (const timer of state.presenceTimers) {
            window.clearTimeout(timer);
        }
        state.presenceTimers.length = 0;
        state.started = false;
        state.screenSharing = false;

        for (const tile of state.tileCache.values()) {
            const video = tile.querySelector("video");
            if (video) video.srcObject = null;
            tile.remove();
        }
        state.tileCache.clear();
        canvas?.classList.remove("has-filmstrip", "screen-share", "screen-share-mode");

        await state.signaling?.stop();
        state.signaling = null;

        if (markLeft) {
            postState("left");
            post("left");
        }
    }

    async function applyTeacherMute(studentId) {
        if (!isTeacher()) return;
        await state.signaling?.send("teacher-mute", {}, studentId);
    }

    async function kickByTeacher(studentId) {
        if (!isTeacher()) return;
        await state.signaling?.send("teacher-kick", {}, studentId);
        closePeer(studentId, true);
    }

    function setLayoutMode(mode) {
        state.screenSharing = mode === "screen-share" ? state.screenSharing : state.screenSharing;
        canvas?.classList.toggle("screen-share", mode === "screen-share" || state.screenSharing);
        renderLayout();
    }

    window.addEventListener("beforeunload", () => void cleanup(true, false));

    window.CourseGuardWebRtcClassroom = {
        start,
        cleanup,
        setMicEnabled,
        setCameraEnabled,
        startScreenShare,
        stopScreenShare,
        toggleScreenShare,
        setLayoutMode,
        applyTeacherMute,
        kickByTeacher
    };

    post("loaded");
})();
