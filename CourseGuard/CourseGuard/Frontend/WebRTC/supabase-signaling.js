(() => {
    class SupabaseTableSignaling {
        constructor(config, logger, options = {}) {
            if (!window.supabase?.createClient) {
                throw new Error("Supabase JS SDK chưa được tải.");
            }

            this.config = config;
            this.log = logger ?? (() => { });
            this.client = window.supabase.createClient(config.supabaseUrl, config.supabaseAnonKey, {
                auth: { persistSession: false, autoRefreshToken: false }
            });
            this.tableName = "classroom_webrtc_signals";
            this.channel = null;
            this.handlers = new Set();
            this.seenSignalIds = new Set();
            this.joinedAt = options.joinedAt || new Date().toISOString();
            this.startedAt = new Date(Date.now() - 120000).toISOString(); // Fetch signals from 2 minutes ago to prevent clock skew
        }

        get roomId() {
            return this.config.roomId;
        }

        get senderId() {
            return String(this.config.userId);
        }

        async start() {
            if (this.channel) {
                return;
            }

            const subscribed = new Promise((resolve, reject) => {
                const timeoutId = window.setTimeout(() => {
                    reject(new Error("Supabase Realtime không phản hồi khi tham gia phòng."));
                }, 10000);

                this.channel = this.client
                    .channel(`courseguard-webrtc-${this.roomId}-${this.senderId}-${Date.now()}`)
                    .on(
                        "postgres_changes",
                        {
                            event: "INSERT",
                            schema: "public",
                            table: this.tableName,
                            filter: `room_id=eq.${this.roomId}`
                        },
                        (payload) => this.#handleSignal(payload.new)
                    )
                    .subscribe((status) => {
                        this.log(`Supabase Realtime: ${status}`);
                        if (status === "SUBSCRIBED") {
                            window.clearTimeout(timeoutId);
                            resolve(status);
                        } else if (status === "CHANNEL_ERROR" || status === "TIMED_OUT" || status === "CLOSED") {
                            window.clearTimeout(timeoutId);
                            reject(new Error(`Supabase Realtime lỗi: ${status}`));
                        }
                    });
            });

            await subscribed;
            await this.#hydrateRecentSignals();
        }

        async #hydrateRecentSignals() {
            const { data, error } = await this.client
                .from(this.tableName)
                .select("*")
                .eq("room_id", this.roomId)
                .gte("created_at", this.startedAt)
                .order("created_at", { ascending: true })
                .limit(200);

            if (error) {
                this.log(`Không đọc được signaling gần đây: ${error.message}`);
                return;
            }

            for (const row of data || []) {
                this.#handleSignal(row);
            }
        }

        async stop() {
            if (!this.channel) {
                return;
            }

            await this.client.removeChannel(this.channel);
            this.channel = null;
            this.handlers.clear();
            this.seenSignalIds.clear();
        }

        onSignal(handler) {
            this.handlers.add(handler);
            return () => this.handlers.delete(handler);
        }

        async send(type, payload = {}, receiverId = null) {
            const row = {
                room_id: this.roomId,
                session_id: this.config.sessionId,
                sender_id: this.senderId,
                sender_role: this.config.role,
                receiver_id: receiverId === null || receiverId === undefined ? null : String(receiverId),
                signal_type: type,
                payload,
                created_at: new Date().toISOString()
            };

            const { error } = await this.client.from(this.tableName).insert(row);
            if (error) {
                throw new Error(`Không gửi được signaling '${type}': ${error.message}`);
            }
        }

        #handleSignal(row) {
            if (!row || this.seenSignalIds.has(row.id)) {
                return;
            }

            this.seenSignalIds.add(row.id);

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
    }

    window.CourseGuardSupabaseTableSignaling = SupabaseTableSignaling;
})();
