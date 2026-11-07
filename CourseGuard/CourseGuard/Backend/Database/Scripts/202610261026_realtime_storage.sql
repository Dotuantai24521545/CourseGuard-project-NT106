
INSERT INTO storage.buckets (id, name, public) VALUES ('avatars', 'avatars', true) ON CONFLICT DO NOTHING;
INSERT INTO storage.buckets (id, name, public) VALUES ('chat-media', 'chat-media', false) ON CONFLICT DO NOTHING;
INSERT INTO storage.buckets (id, name, public) VALUES ('course-materials', 'course-materials', false) ON CONFLICT DO NOTHING;
INSERT INTO storage.buckets (id, name, public) VALUES ('monitor-violations', 'monitor-violations', false) ON CONFLICT DO NOTHING;

CREATE POLICY "Allow public read access" ON storage.objects
FOR SELECT USING (
    bucket_id IN ('avatars', 'chat-media', 'course-materials', 'monitor-violations')
);

CREATE POLICY "Allow public insert access" ON storage.objects
FOR INSERT WITH CHECK (
    bucket_id IN ('avatars', 'chat-media', 'course-materials', 'monitor-violations')
);