(() => {
    class CourseGuardRelaySignaling {
        constructor(config, logger, options = {}) {
            this.config = config;
            this.log = logger ?? (() => { });
            this.handlers = new Set();
            this.reconnectHandlers = new Set();
            this.terminatedHandlers = new Set();
            this.seenSignalIds = new Set();
            this.maxSeenSignalIds = 2000;
            this.everConnected = false;
            this.joinedAt = options.joinedAt || new Date().toISOString();
            this.socket = null;
            this.started = false;
            this.manuallyStopped = false;
            this.reconnectAttempts = 0;
            // Backoff 1s, 2s, 4s, 8s, then every 10s: covers a Wi-Fi switch or a short server restart.
            this.maxReconnectAttempts = 8;
            this.reconnectDelayMs = 1000;
            this.maxReconnectDelayMs = 10000;
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
            this.reconnectHandlers.clear();
            this.terminatedHandlers.clear();
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

        /** Called after the socket comes back; peers may have dropped us meanwhile, so re-announce. */
        onReconnected(handler) {
            this.reconnectHandlers.add(handler);
            return () => this.reconnectHandlers.delete(handler);
        }

        /** Called when the connection ends for good (room closed, replaced, retries exhausted). */
        onTerminated(handler) {
            this.terminatedHandlers.add(handler);
            return () => this.terminatedHandlers.delete(handler);
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

            if (this.manuallyStopped) {
                return;
            }
            this.pendingMessages.push(message);
            // While a reconnect is scheduled, queue only: a second parallel connect would open a duplicate socket.
            if (this.reconnectAttempts === 0 && (!this.socket || this.socket.readyState === WebSocket.CLOSED)) {
                await this.#connect();
            }
        }

        async #connect() {
            const url = await this.#buildConnectionUrl();
            await new Promise((resolve, reject) => {
                const socket = new WebSocket(url);
                this.socket = socket;

                const timeoutId = window.setTimeout(() => {
                    reject(new Error("Máy chủ lớp học không phản hồi khi kết nối."));
                    try {
                        socket.close();
                    } catch {
                    }
                }, 10000);

                socket.onopen = () => {
                    window.clearTimeout(timeoutId);
                    if (this.socket !== socket) {
                        return;
                    }
                    const isReconnect = this.everConnected;
                    this.everConnected = true;
                    this.started = true;
                    this.reconnectAttempts = 0;
                    this.log(isReconnect ? "Đã kết nối lại máy chủ lớp học." : "Đã kết nối máy chủ lớp học.");
                    this.#flushPendingMessages();
                    if (isReconnect) {
                        for (const handler of this.reconnectHandlers) {
                            void handler();
                        }
                    }
                    resolve();
                };

                socket.onmessage = (event) => {
                    void this.#handleMessage(event.data);
                };

                socket.onerror = () => {
                    this.log("Kết nối WebSocket tới máy chủ lớp học bị lỗi.");
                };

                socket.onclose = (event) => {
                    window.clearTimeout(timeoutId);
                    // A stale socket closing after a newer one opened must not start another reconnect loop.
                    if (this.socket !== socket) {
                        return;
                    }
                    const wasOpen = this.started;
                    this.started = false;
                    if (this.manuallyStopped) {
                        return;
                    }
                    const terminal = CourseGuardRelaySignaling.terminalCloseReason(event?.code);
                    if (terminal) {
                        this.manuallyStopped = true;
                        this.#notifyTerminated(event.code, terminal);
                        reject(new Error(terminal));
                        return;
                    }
                    if (!wasOpen) {
                        // Never opened: let the caller (start or #scheduleReconnect) decide what to do.
                        reject(new Error("Không kết nối được máy chủ lớp học."));
                        return;
                    }
                    void this.#scheduleReconnect();
                };
            });
        }

        async #buildConnectionUrl() {
            if (this.config.apiMode) {
                const ticket = await new Promise((resolve, reject) => {
                    const requestId = crypto.randomUUID();
                    let timer;
                    const handler = event => {
                        const row = event.data;
                        if (row?.type !== "classroom-ticket" || row.requestId !== requestId) return;
                        clearTimeout(timer);
                        window.chrome.webview.removeEventListener("message", handler);
                        row.error ? reject(new Error(row.error)) : resolve(row.ticket);
                    };
                    window.chrome.webview.addEventListener("message", handler);
                    timer = setTimeout(() => { window.chrome.webview.removeEventListener("message", handler); reject(new Error("Không nhận được quyền kết nối lớp học.")); }, 10000);
                    window.chrome.webview.postMessage({ type: "request-classroom-ticket", requestId });
                });
                return `${this.config.relayWsUrl}?ticket=${encodeURIComponent(ticket)}`;
            }
            const rawUrl = String(this.config.relayWsUrl || "").trim();
            if (!rawUrl) {
                throw new Error("Thiếu RELAY_WS_URL cho relay lớp học (chế độ cũ).");
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
                const reason = `Mất kết nối máy chủ lớp học sau ${this.maxReconnectAttempts} lần thử lại.`;
                this.log(reason);
                this.manuallyStopped = true;
                this.#notifyTerminated(0, reason);
                return;
            }

            this.reconnectAttempts += 1;
            const attempt = this.reconnectAttempts;
            const delay = Math.min(this.reconnectDelayMs * 2 ** (attempt - 1), this.maxReconnectDelayMs);
            this.log(`Đang kết nối lại máy chủ lớp học (${attempt}/${this.maxReconnectAttempts})...`);
            await new Promise((resolve) => window.setTimeout(resolve, delay));

            if (this.manuallyStopped) {
                return;
            }

            try {
                await this.#connect();
            } catch (error) {
                this.log(`Kết nối lại máy chủ lớp học thất bại: ${error?.message || error}`);
                // A refused ticket or a socket that never opened does not reach the "was open" branch of
                // onclose, so the retry loop continues from here.
                if (!this.manuallyStopped && !this.started) {
                    void this.#scheduleReconnect();
                }
            }
        }

        #notifyTerminated(code, reason) {
            for (const handler of this.terminatedHandlers) {
                void handler({ code, reason });
            }
        }

        /** Close codes sent by CourseGuard.Server ClassroomApi: reconnecting would be refused or would kick the other window. */
        static terminalCloseReason(code) {
            switch (code) {
                case 4000:
                    return "Tài khoản này đã vào lớp học ở một cửa sổ khác.";
                case 4001:
                    return "Giáo viên đã đóng lớp học.";
                case 1008:
                    return "Lớp học đã đủ số kết nối tối đa.";
                default:
                    return null;
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
            if (this.seenSignalIds.size > this.maxSeenSignalIds) {
                // Sets iterate in insertion order: drop the oldest id so a 2-hour class does not grow forever.
                this.seenSignalIds.delete(this.seenSignalIds.values().next().value);
            }

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
