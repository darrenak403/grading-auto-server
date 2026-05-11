using Microsoft.AspNetCore.Mvc;
using givenAPI.Models;
using System.Linq;

namespace givenAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InstructorsController : ControllerBase
    {
        // 1. GET /api/instructors/search?name={name}&expertise={expertise}
        // Trả về danh sách giảng viên kèm số lượng khóa học đảm nhận
        [HttpGet("search")]
        public IActionResult Search(string name = "", string expertise = "")
        {
            var result = DataInitializer.Instructors
                .Where(i => (string.IsNullOrEmpty(name) || i.FullName.Contains(name, StringComparison.OrdinalIgnoreCase))
                         && (string.IsNullOrEmpty(expertise) || i.Expertise.Contains(expertise, StringComparison.OrdinalIgnoreCase)))
                .Select(i => new {
                    InstructorId = i.InstructorId,
                    FullName = i.FullName,
                    Expertise = i.Expertise,
                    HireDate = i.HireDate,
                    // Đếm tổng số khóa học từ bảng trung gian CourseAssignments
                    TotalCourses = DataInitializer.CourseAssignments.Count(ca => ca.InstructorId == i.InstructorId)
                }).ToList();

            return Ok(result);
        }

        // 2. GET /api/instructors/{instructorId}
        // Trả về thông tin chi tiết giảng viên và danh sách các môn học được phân công
        [HttpGet("{instructorId}")]
        public IActionResult GetDetails(int instructorId)
        {
            var instructor = DataInitializer.Instructors.FirstOrDefault(i => i.InstructorId == instructorId);
            if (instructor == null) return NotFound(new { Message = "Instructor not found." });

            // Lấy danh sách môn học thông qua bảng CourseAssignments
            var assignedCourses = from ca in DataInitializer.CourseAssignments
                                  join c in DataInitializer.Courses on ca.CourseId equals c.CourseId
                                  where ca.InstructorId == instructorId
                                  select new
                                  {
                                      CourseId = c.CourseId,
                                      CourseName = c.CourseName,
                                      Credits = c.Credits
                                  };

            return Ok(new
            {
                InstructorId = instructor.InstructorId,
                FullName = instructor.FullName,
                Expertise = instructor.Expertise,
                HireDate = instructor.HireDate,
                Courses = assignedCourses.ToList()
            });
        }
    }
}