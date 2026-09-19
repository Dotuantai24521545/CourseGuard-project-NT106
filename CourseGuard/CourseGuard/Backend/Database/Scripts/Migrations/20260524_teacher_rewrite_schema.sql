CREATE TABLE IF NOT EXISTS teacher_profiles (
    user_id INTEGER PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    teacher_code VARCHAR(32) UNIQUE,
    phone TEXT,
    gender TEXT,
    birth_date DATE,
    address TEXT,
    major TEXT,
    degrees TEXT,
    bio TEXT,
    avatar_path TEXT,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

ALTER TABLE teacher_profiles
    ADD COLUMN IF NOT EXISTS avatar_path TEXT;

INSERT INTO teacher_profiles (user_id, teacher_code)
SELECT u.id, 'GV' || LPAD(u.id::text, 5, '0')
FROM users u
JOIN roles r ON r.id = u.role_id
WHERE UPPER(r.name) = 'TEACHER'
ON CONFLICT (user_id) DO NOTHING;

UPDATE teacher_profiles
SET teacher_code = 'GV' || LPAD(user_id::text, 5, '0')
WHERE teacher_code IS NULL OR teacher_code = '';

ALTER TABLE teacher_profiles
    ALTER COLUMN teacher_code SET NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS idx_teacher_profiles_teacher_code
    ON teacher_profiles(teacher_code);

CREATE TABLE IF NOT EXISTS teacher_lessons (
    id SERIAL PRIMARY KEY,
    course_id INT NOT NULL REFERENCES courses(id) ON DELETE CASCADE,
    title VARCHAR(200) NOT NULL,
    content TEXT,
    publish_at TIMESTAMP,
    status VARCHAR(20) DEFAULT 'DRAFT',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS teacher_assignments (
    id SERIAL PRIMARY KEY,
    course_id INT NOT NULL REFERENCES courses(id) ON DELETE CASCADE,
    title VARCHAR(200) NOT NULL,
    description TEXT,
    due_at TIMESTAMP,
    status VARCHAR(20) DEFAULT 'OPEN',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_teacher_lessons_course ON teacher_lessons(course_id);
CREATE INDEX IF NOT EXISTS idx_teacher_lessons_status ON teacher_lessons(status);
CREATE INDEX IF NOT EXISTS idx_teacher_assignments_course ON teacher_assignments(course_id);
CREATE INDEX IF NOT EXISTS idx_teacher_assignments_due_at ON teacher_assignments(due_at);
CREATE INDEX IF NOT EXISTS idx_teacher_assignments_status ON teacher_assignments(status);
