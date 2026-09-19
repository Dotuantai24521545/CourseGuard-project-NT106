(() => {
    class CourseGuardRelaySignaling {
        constructor(config, logger, options = {}) {
            this.config = config;
            this.log = logger ?? (() => { });
            this.handlers = new Set();
            this.seenSignalIds = new Set();
            this.joinedAt = options.joinedAt || new Date().toISOString();
            this.socket = null;
            this.started = false;
            this.manuallyStopped = false;
            this.reconnectAttempts = 0;
            this.maxReconnectAttempts = 3;
            this.reconnectDelayMs = 2000;
            this.pendingMessages = [];
        }

        get senderId() {
            return String(this.config.userId);
        }

        async start() {
            if (this.socket && this.socket.readyState === WebSocket.OPEN) {
                return;
            }

            this.manuallyStopped = false;
            await this.#connect();
        }

        async stop() {
            this.manuallyStopped = true;
            this.started = false;
            this.pendingMessages.length = 0;
            this.handlers.clear();
            this.seenSignalIds.clear();

            if (!this.socket) {
                return;
            }

            const socket = this.socket;
            this.socket = null;
            if (socket.readyState === WebSocket.OPEN || socket.readyState === WebSocket.CONNECTING) {
                socket.close(1000, "CourseGuard classroom leaving");
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

            this.pendingMessages.push(message);
            if (!this.socket || this.socket.readyState === WebSocket.CLOSED) {
                await this.#connect();
            }
        }

        async #connect() {
            const url = this.#buildConnectionUrl();
            await new Promise((resolve, reject) => {
                const socket = new WebSocket(url);
                this.socket = socket;

                const timeoutId = window.setTimeout(() => {
                    reject(new Error("Railway Classroom Relay không phản hồi khi kết nối."));
                    try {
                        socket.close();
                    } catch {
                    }
                }, 10000);

                socket.onopen = () => {
                    window.clearTimeout(timeoutId);
                    this.started = true;
                    this.reconnectAttempts = 0;
                    this.log("Đã kết nối Railway Classroom Relay.");
                    this.#flushPendingMessages();
                    resolve();
                };

                socket.onmessage = (event) => {
                    void this.#handleMessage(event.data);
                };

                socket.onerror = () => {
                    this.log("Railway Classroom Relay báo lỗi WebSocket.");
                };

                socket.onclose = () => {
                    window.clearTimeout(timeoutId);
                    this.started = false;
                    if (!this.manuallyStopped) {
                        void this.#scheduleReconnect();
                    }
                };
            });
        }

        #buildConnectionUrl() {
            const rawUrl = String(this.config.relayWsUrl || "").trim();
            if (!rawUrl) {
                throw new Error("Thiếu RELAY_WS_URL cho Railway Classroom Relay.");
            }

            const baseUrl = rawUrl.endsWith("/relay")
                ? rawUrl.slice(0, -"/relay".length)
                : rawUrl.replace(/\/$/, "");
            const role = encodeURIComponent(String(this.config.role || ""));
            const classId = encodeURIComponent(String(this.config.sessionId || ""));
            const userId = encodeURIComponent(this.senderId);
            const name = encodeURIComponent(this.config.displayName || this.config.role || "User");
            return `${baseUrl}/classroom-relay?role=${role}&classId=${classId}&userId=${userId}&name=${name}`;
        }

        async #scheduleReconnect() {
            if (this.reconnectAttempts >= this.maxReconnectAttempts) {
                this.log("Railway Classroom Relay đã ngắt kết nối sau 3 lần thử lại.");
                return;
            }

            this.reconnectAttempts += 1;
            const attempt = this.reconnectAttempts;
            this.log(`Đang kết nối lại Railway Classroom Relay (${attempt}/${this.maxReconnectAttempts})...`);
            await new Promise((resolve) => window.setTimeout(resolve, this.reconnectDelayMs));

            if (this.manuallyStopped) {
                return;
            }

            try {
                await this.#connect();
            } catch (error) {
                this.log(`Kết nối lại Railway Classroom Relay thất bại: ${error?.message || error}`);
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
                this.log("Bỏ qua relay message không phải JSON.");
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
