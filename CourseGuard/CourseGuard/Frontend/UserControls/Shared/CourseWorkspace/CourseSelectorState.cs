using System;
using System.Collections.Generic;

namespace CourseGuard.Frontend.UserControls.Shared.CourseWorkspace;

public static class CourseSelectorState
{
    public static IReadOnlyList<CourseSelectorItem> BuildRecentCourses(
        IReadOnlyList<CourseSelectorItem> courses,
        IReadOnlyList<int> recentCourseIds,
        int maxCount)
    {
        if (courses.Count == 0 || recentCourseIds.Count == 0 || maxCount <= 0)
            return Array.Empty<CourseSelectorItem>();

        Dictionary<int, CourseSelectorItem> coursesById = new();
        foreach (CourseSelectorItem course in courses)
        {
            if (!coursesById.ContainsKey(course.CourseId))
                coursesById.Add(course.CourseId, course);
        }

        List<CourseSelectorItem> recent = new();
        HashSet<int> seen = new();
        foreach (int courseId in recentCourseIds)
        {
            if (!seen.Add(courseId))
                continue;

            if (!coursesById.TryGetValue(courseId, out CourseSelectorItem? course))
                continue;

            recent.Add(course);
            if (recent.Count == maxCount)
                break;
        }

        return recent;
    }

    public static CourseSelectorItem? FindInitialCourse(
        IReadOnlyList<CourseSelectorItem> courses,
        int? preferredCourseId)
    {
        if (courses.Count == 0)
            return null;

        if (preferredCourseId.HasValue)
        {
            foreach (CourseSelectorItem course in courses)
            {
                if (course.CourseId == preferredCourseId.Value)
                    return course;
            }
        }

        return courses[0];
    }
}
