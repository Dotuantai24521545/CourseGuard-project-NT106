(() => {
    class CourseGuardRelaySignaling {
        constructor(config, logger, options = {}) {
            this.config = config;
            this.log = logger ?? (() => { });
            this.onReconnected = options.onReconnected ?? null;
            this.handlers = new Set();
            this.seenSignalIds = new Set();
            this.joinedAt = options.joinedAt || new Date().toISOString();
            this.socket = null;
            this.started = false;
            this.manuallyStopped = false;
            this.isReconnecting = false;
            this.reconnectAttempts = 0;
            this.maxReconnectAttempts = 15;
            this.reconnectDelayMs = 2000;
            this.pendingMessages = [];
            this.reconnectTimer = null;
            this.hadInitialConnection = false;
        }

        get senderId() {
            return String(this.config.userId);
        }

        async start() {
            if (this.socket && (this.socket.readyState === WebSocket.OPEN || this.socket.readyState === WebSocket.CONNECTING)) {
                return;
            }

            this.manuallyStopped = false;
            await this.#connect();
        }

        async stop() {
            this.manuallyStopped = true;
            this.started = false;
            this.isReconnecting = false;
            if (this.reconnectTimer) {
                window.clearTimeout(this.reconnectTimer);
                this.reconnectTimer = null;
            }
            this.pendingMessages.length = 0;
            this.handlers.clear();
            this.seenSignalIds.clear();

            if (!this.socket) {
                return;
            }

            const socket = this.socket;
            this.socket = null;
            socket.onopen = null;
            socket.onmessage = null;
            socket.onerror = null;
            socket.onclose = null;
            if (socket.readyState === WebSocket.OPEN || socket.readyState === WebSocket.CONNECTING) {
                try {
                    socket.close(1000, "CourseGuard classroom leaving");
                } catch {
                }
            }
        }

        onSignal(handler) {
            this.handlers.add(handler);
            return () => this.handlers.delete(handler);
        }

        async send(type, payload = {}, receiverId = null) {
            const row = {
                id: `${this.senderId}-${Date.now()}-${Math.random().toString(16).slice(2)}`,
                room_id: this.config.roomId,
                session_id: this.config.sessionId,
                sender_id: this.senderId,
                sender_role: this.config.role,
                sender_name: this.config.displayName || this.config.role || "User",
                receiver_id: receiverId === null || receiverId === undefined ? null : String(receiverId),
                signal_type: type,
                payload,
                created_at: new Date().toISOString()
            };

            const message = JSON.stringify(row);
            if (this.socket?.readyState === WebSocket.OPEN) {
                this.socket.send(message);
                return;
            }

            if (this.pendingMessages.length < 100) {
                this.pendingMessages.push(message);
            }
            if (!this.manuallyStopped && (!this.socket || this.socket.readyState === WebSocket.CLOSED)) {
                void this.#scheduleReconnect();
            }
        }

        async #connect() {
            const url = this.#buildConnectionUrl();
            await new Promise((resolve, reject) => {
                let settled = false;
                const socket = new WebSocket(url);
                this.socket = socket;

                const timeoutId = window.setTimeout(() => {
                    if (settled) return;
                    settled = true;
                    try { socket.close(); } catch { }
                    reject(new Error("Classroom Relay không phản hồi khi kết nối."));
                }, 10000);

                socket.onopen = () => {
                    if (settled) return;
                    settled = true;
                    window.clearTimeout(timeoutId);
                    const wasReconnect = this.hadInitialConnection;
                    this.hadInitialConnection = true;
                    this.started = true;
                    this.reconnectAttempts = 0;
                    this.log("Đã kết nối Classroom Relay.");
                    this.#flushPendingMessages();
                    if (wasReconnect && typeof this.onReconnected === "function") {
                        this.onReconnected();
                    }
                    resolve();
                };

                socket.onmessage = (event) => {
                    void this.#handleMessage(event.data);
                };

                socket.onerror = () => {
                    this.log("Classroom Relay báo lỗi WebSocket.");
                };

                socket.onclose = () => {
                    window.clearTimeout(timeoutId);
                    this.started = false;
                    if (!settled) {
                        settled = true;
                        reject(new Error("Kết nối Classroom Relay đã bị đóng."));
                        return;
                    }
                    if (!this.manuallyStopped) {
                        void this.#scheduleReconnect();
                    }
                };
            });
        }

        #buildConnectionUrl() {
            const rawUrl = String(this.config.relayWsUrl || "").trim();
            if (!rawUrl) {
                throw new Error("Thiếu RELAY_WS_URL cho Classroom Relay.");
            }

            let baseUrl = rawUrl.replace(/\/+$/, "");
            if (baseUrl.toLowerCase().endsWith("/relay")) {
                baseUrl = baseUrl.slice(0, -"/relay".length).replace(/\/+$/, "");
            }

            const role = encodeURIComponent(String(this.config.role || ""));
            const classId = encodeURIComponent(String(this.config.sessionId || ""));
            const userId = encodeURIComponent(this.senderId);
            const name = encodeURIComponent(this.config.displayName || this.config.role || "User");
            return `${baseUrl}/classroom-relay?role=${role}&classId=${classId}&userId=${userId}&name=${name}`;
        }

        async #scheduleReconnect() {
            if (this.manuallyStopped || this.isReconnecting) {
                return;
            }

            this.isReconnecting = true;
            try {
                while (!this.manuallyStopped && this.reconnectAttempts < this.maxReconnectAttempts) {
                    this.reconnectAttempts += 1;
                    const attempt = this.reconnectAttempts;
                    const delay = Math.min(this.reconnectDelayMs * Math.pow(1.5, attempt - 1), 15000);
                    this.log(`Đang kết nối lại Classroom Relay (${attempt}/${this.maxReconnectAttempts})...`);

                    await new Promise((resolve) => {
                        this.reconnectTimer = window.setTimeout(resolve, delay);
                    });

                    if (this.manuallyStopped) {
                        return;
                    }

                    try {
                        await this.#connect();
                        return;
                    } catch (error) {
                        this.log(`Kết nối lại Classroom Relay thất bại: ${error?.message || error}`);
                    }
                }
            } finally {
                this.isReconnecting = false;
            }
        }

        #flushPendingMessages() {
            if (!this.socket || this.socket.readyState !== WebSocket.OPEN) {
                return;
            }

            while (this.pendingMessages.length > 0) {
                this.socket.send(this.pendingMessages.shift());
            }
        }

        async #handleMessage(data) {
            const text = await this.#readMessageText(data);
            if (!text) {
                return;
            }

            let row;
            try {
                row = JSON.parse(text);
            } catch {
                return;
            }

            row = this.#normalizeSignal(row);
            if (!row) {
                return;
            }

            const signalId = row.id || `${row.sender_id}:${row.signal_type}:${row.created_at}:${row.receiver_id || ""}`;
            if (this.seenSignalIds.has(signalId)) {
                return;
            }
            if (this.seenSignalIds.size > 2000) {
                this.seenSignalIds.clear();
            }
            this.seenSignalIds.add(signalId);

            if (String(row.sender_id) === this.senderId) {
                return;
            }

            if (row.receiver_id && String(row.receiver_id) !== this.senderId) {
                return;
            }

            for (const handler of this.handlers) {
                void handler(row);
            }
        }

        async #readMessageText(data) {
            if (typeof data === "string") {
                return data;
            }

            if (data instanceof Blob) {
                return await data.text();
            }

            if (data instanceof ArrayBuffer) {
                return new TextDecoder().decode(data);
            }

            return "";
        }

        #normalizeSignal(row) {
            if (row.signal_type) {
                return {
                    ...row,
                    sender_id: String(row.sender_id ?? row.senderId ?? ""),
                    sender_role: String(row.sender_role ?? row.senderRole ?? "").toLowerCase(),
                    sender_name: row.sender_name ?? row.senderName ?? row.payload?.n ?? "",
                    receiver_id: row.receiver_id === undefined || row.receiver_id === null ? null : String(row.receiver_id),
                    payload: row.payload || {}
                };
            }

            if (row.type === "JOIN_ROOM" || row.type === "LEAVE_ROOM") {
                return {
                    id: row.id || `${row.senderId || row.sender_id}-${row.type}-${row.timestampUnixMs || Date.now()}`,
                    room_id: this.config.roomId,
                    session_id: row.sessionId || row.session_id || this.config.sessionId,
                    sender_id: String(row.senderId ?? row.sender_id ?? ""),
                    sender_role: String(row.senderRole ?? row.sender_role ?? "").toLowerCase(),
                    sender_name: row.senderName ?? row.sender_name ?? "",
                    receiver_id: null,
                    signal_type: row.type === "JOIN_ROOM" ? "participant-join" : "participant-leave",
                    payload: row.payload || {},
                    created_at: row.created_at || new Date(row.timestampUnixMs || Date.now()).toISOString()
                };
            }

            return null;
        }
    }

    window.CourseGuardRelaySignaling = CourseGuardRelaySignaling;
})();