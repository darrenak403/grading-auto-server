using PRN232.LMS.Repositories.Context;
using PRN232.LMS.Repositories.Entities;
using PRN232.LMS.Repositories.Interfaces;

namespace PRN232.LMS.Repositories.Implementations;

public class StudentRepository : GenericRepository<Student>, IStudentRepository
{
    public StudentRepository(LmsDbContext context) : base(context) { }
}
