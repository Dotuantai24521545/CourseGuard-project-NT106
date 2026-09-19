namespace CourseGuard.Frontend.Helpers
{
    public static class NextActionKinds
    {
        public const string None = "None";
        public const string SubmitCourseApproval = "SubmitCourseApproval";
        public const string RequestCourseEnrollment = "RequestCourseEnrollment";
        public const string DropCourseEnrollment = "DropCourseEnrollment";
        public const string OpenStudentLessonsTab = "OpenStudentLessonsTab";
        public const string OpenStudentDocumentsTab = "OpenStudentDocumentsTab";
        public const string OpenStudentScheduleTab = "OpenStudentScheduleTab";
        public const string OpenCurrentLesson = "OpenCurrentLesson";
        public const string DownloadCurrentMaterial = "DownloadCurrentMaterial";
        public const string OpenTeacherLessonsTab = "OpenTeacherLessonsTab";
        public const string OpenTeacherMaterialsTab = "OpenTeacherMaterialsTab";
        public const string OpenTeacherScheduleTab = "OpenTeacherScheduleTab";
        public const string DownloadTeacherLessonFile = "DownloadTeacherLessonFile";
        public const string DownloadTeacherMaterial = "DownloadTeacherMaterial";
        public const string JoinStudentClassroom = "JoinStudentClassroom";
        public const string OpenTeacherClassroom = "OpenTeacherClassroom";
    }

    public static class NavigationTargets
    {
        public const string None = "None";
        public const string StudentCourseList = "StudentCourseList";
        public const string StudentMyCourses = "StudentMyCourses";
        public const string StudentLessons = "StudentLessons";
        public const string StudentDocuments = "StudentDocuments";
        public const string StudentSchedule = "StudentSchedule";
        public const string TeacherCourses = "TeacherCourses";
        public const string TeacherLessons = "TeacherLessons";
        public const string TeacherMaterials = "TeacherMaterials";
        public const string TeacherSchedule = "TeacherSchedule";
    }
}
