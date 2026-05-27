using PRN232.LMS.Repositories.Context;
using PRN232.LMS.Repositories.Entities;
using PRN232.LMS.Repositories.Interfaces;

namespace PRN232.LMS.Repositories.Implementations;

public class CourseRepository : GenericRepository<Course>, ICourseRepository
{
    public CourseRepository(LmsDbContext context) : base(context) { }
}
