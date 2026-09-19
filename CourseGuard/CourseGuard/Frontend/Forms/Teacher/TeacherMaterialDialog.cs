using System.Collections.Generic;
using CourseGuard.Backend.Models;

namespace CourseGuard.Frontend.Forms.Teacher
{
    public partial class TeacherMaterialDialog : TeacherSimpleItemDialog
    {
        public TeacherMaterialDialog(IEnumerable<TeacherCourseModel> courses) : base("Tài liệu", courses, status: "ACTIVE") { }
    }
}
