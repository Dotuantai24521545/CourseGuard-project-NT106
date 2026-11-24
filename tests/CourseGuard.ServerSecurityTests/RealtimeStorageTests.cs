// Tests for chat upload validation, classroom connection roster and ICE configuration (member 4 scope).
static class RealtimeStorageTests
{
    public static void Run(Action<bool, string> check)
    {
        UploadPolicy(check);
        Roster(check);
        SignalRules(check);
        IceServers(check);
    }

    static readonly byte[] Png = { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13 };
    static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 16 };

    static void UploadPolicy(Action<bool, string> check)
    {
        var pdf = System.Text.Encoding.ASCII.GetBytes("%PDF-1.7 test");
        var ok = ChatUploadPolicy.CheckFile("bai-tap.pdf", "", pdf);
        check(ok.Allowed && ok.MimeType == "application/pdf", "chat file: allowed extension accepted, MIME derived on server");
        check(ChatUploadPolicy.CheckFile("C:\\Users\\a\\..\\bai.pdf", null, pdf).FileName == "bai.pdf", "chat file: client path stripped from file name");
        check(ChatUploadPolicy.CheckFile("../../etc/bai.pdf", null, pdf).FileName == "bai.pdf", "chat file: traversal segments stripped");
        check(!ChatUploadPolicy.CheckFile("virus.exe", "", pdf).Allowed, "chat file: .exe rejected");
        check(!ChatUploadPolicy.CheckFile("script.ps1", "", pdf).Allowed, "chat file: script rejected");
        check(!ChatUploadPolicy.CheckFile("noext", "", pdf).Allowed, "chat file: missing extension rejected");
        check(!ChatUploadPolicy.CheckFile("a.pdf", "", Array.Empty<byte>()).Allowed, "chat file: empty file rejected");
        check(!ChatUploadPolicy.CheckFile("a.pdf", "", null).Allowed, "chat file: null bytes rejected (no 500)");
        check(!ChatUploadPolicy.CheckFile("a.pdf", "", new byte[ChatUploadPolicy.MaxFileBytes + 1]).Allowed, "chat file: over 5 MB rejected");
        check(ChatUploadPolicy.CheckFile("a.pdf", "", new byte[ChatUploadPolicy.MaxFileBytes]).Allowed, "chat file: exactly 5 MB accepted");
        check(!ChatUploadPolicy.CheckFile("a.pdf", new string('x', 4001), pdf).Allowed, "chat file: caption over 4000 chars rejected");
        check(!ChatUploadPolicy.CheckFile(new string('a', 251) + ".pdf", "", pdf).Allowed, "chat file: file name over 250 chars rejected");

        var exe = new byte[0x100]; exe[0] = (byte)'M'; exe[1] = (byte)'Z'; BitConverter.GetBytes(0x80).CopyTo(exe, 0x3C);
        exe[0x80] = (byte)'P'; exe[0x81] = (byte)'E';
        check(!ChatUploadPolicy.CheckFile("tai-lieu.pdf", "", exe).Allowed, "chat file: renamed Windows executable rejected");
        var mzText = System.Text.Encoding.ASCII.GetBytes("MZ company report " + new string('x', 100));
        check(ChatUploadPolicy.CheckFile("report.txt", "", mzText).Allowed, "chat file: text that merely starts with MZ accepted");

        check(ChatUploadPolicy.CheckFile("anh.PNG", "", Png).Allowed, "chat file: real PNG accepted (case-insensitive extension)");
        check(!ChatUploadPolicy.CheckFile("anh.png", "", pdf).Allowed, "chat file: .png that is not a PNG rejected");
        check(ChatUploadPolicy.CheckFile("anh.jpg", "", Jpeg).MimeType == "image/jpeg", "chat file: JPEG mapped to image/jpeg");

        var one = new ChatUploadPolicy.ImageItem("a.png", "image/png", Png);
        check(ChatUploadPolicy.CheckImageGroup("", new[] { one }) is null, "chat images: one PNG accepted");
        check(ChatUploadPolicy.CheckImageGroup(null, Enumerable.Repeat(one, 6).ToList()) is not null, "chat images: more than 5 rejected");
        check(ChatUploadPolicy.CheckImageGroup("", null) is not null, "chat images: missing list rejected (no 500)");
        check(ChatUploadPolicy.CheckImageGroup("", new[] { new ChatUploadPolicy.ImageItem("a.png", "image/png", Jpeg) }) is not null, "chat images: MIME/byte mismatch rejected");
        check(ChatUploadPolicy.CheckImageGroup("", new[] { new ChatUploadPolicy.ImageItem("a.gif", "image/gif", Png) }) is not null, "chat images: unsupported MIME rejected");
        var big = new byte[3 * 1024 * 1024]; Png.CopyTo(big, 0);
        var bigItem = new ChatUploadPolicy.ImageItem("b.png", "image/png", big);
        check(ChatUploadPolicy.CheckImageGroup("", new[] { bigItem, bigItem }) is not null, "chat images: total over 5 MB rejected");
    }

    static void Roster(Action<bool, string> check)
    {
        var roster = new ClassroomRoster<string>();
        Guid teacher = Guid.NewGuid(), studentA = Guid.NewGuid(), studentB = Guid.NewGuid(), other = Guid.NewGuid();
        check(roster.Add(teacher, 1, 10, "teacher", 32).Accepted, "classroom: teacher joins");
        check(roster.Add(studentA, 2, 10, "student-a1", 32).Replaced.Count == 0, "classroom: first student connection replaces nothing");
        roster.Add(other, 9, 99, "other-room", 32);

        check(roster.Targets(teacher, 10, null).SequenceEqual(new[] { "student-a1" }), "classroom: broadcast stays in the session and skips the sender");
        check(roster.Targets(studentA, 10, 1).SequenceEqual(new[] { "teacher" }), "classroom: targeted signal reaches only the receiver");
        check(roster.Targets(studentA, 10, 9).Count == 0, "classroom: cannot target a user in another session");

        var reconnect = roster.Add(studentB, 2, 10, "student-a2", 32);
        check(reconnect.Accepted && reconnect.Replaced.SequenceEqual(new[] { "student-a1" }), "classroom: reconnect replaces the old socket of the same user");
        check(roster.Targets(teacher, 10, null).SequenceEqual(new[] { "student-a2" }), "classroom: signals are not delivered twice after reconnect");
        check(!roster.Remove(studentA), "classroom: closing the replaced socket does not announce a leave");
        check(roster.Remove(studentB), "classroom: closing the last socket of a user announces the leave");
        check(!roster.Remove(studentB), "classroom: removing twice is harmless");

        var full = new ClassroomRoster<string>();
        full.Add(Guid.NewGuid(), 1, 5, "t", 2);
        full.Add(Guid.NewGuid(), 2, 5, "s", 2);
        check(!full.Add(Guid.NewGuid(), 3, 5, "x", 2).Accepted, "classroom: room limit enforced");
        check(full.Add(Guid.NewGuid(), 2, 5, "s-again", 2).Accepted, "classroom: a member can reconnect into a full room");

        check(roster.InSession(10, exceptUser: u => u == 1).Count == 0 && roster.InSession(10).SequenceEqual(new[] { "teacher" }),
            "classroom: closing the room selects everyone except the teacher");

        var concurrent = new ClassroomRoster<string>();
        Parallel.For(0, 200, i => concurrent.Add(Guid.NewGuid(), 7, 1, "s" + i, 32));
        check(concurrent.InSession(1).Count == 1, "classroom: concurrent reconnects keep exactly one connection per user");
    }

    static void SignalRules(Action<bool, string> check)
    {
        check(ClassroomSignalRules.IsTeacherOnly("teacher-kick") && ClassroomSignalRules.IsTeacherOnly("teacher-mute"), "classroom: teacher controls are teacher-only");
        check(ClassroomSignalRules.IsTeacherOnly("room-full"), "classroom: students cannot send room-full to eject others");
        check(ClassroomSignalRules.IsTeacherOnly("CLOSE_ROOM"), "classroom: close signals are teacher-only");
        check(!ClassroomSignalRules.IsTeacherOnly("offer") && !ClassroomSignalRules.IsTeacherOnly("participant-join"), "classroom: media negotiation open to students");
    }

    static void IceServers(Action<bool, string> check)
    {
        var defaults = IceServerConfig.Build(null, null, null, null);
        check(defaults.Count == 1 && defaults[0].Urls == IceServerConfig.DefaultStun, "ice: default STUN when nothing configured");
        var turn = IceServerConfig.Build("stun:a:3478, stun:b:3478", "turn:t:3478?transport=udp,turns:t:5349", "user", "secret");
        check(turn.Count == 4 && turn.Count(s => s.Username == "user" && s.Credential == "secret") == 2, "ice: STUN list plus TURN with credentials");
        check(IceServerConfig.Build("", "turn:t:3478", "user", "").All(s => !s.Urls.StartsWith("turn")), "ice: TURN without credential is skipped");
        check(IceServerConfig.Build("http://evil", "https://evil", "u", "p").Count == 0, "ice: non STUN/TURN URLs ignored");
    }
}
