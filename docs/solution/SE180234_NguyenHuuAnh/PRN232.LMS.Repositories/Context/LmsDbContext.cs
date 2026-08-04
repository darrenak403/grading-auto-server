using Microsoft.EntityFrameworkCore;
using PRN232.LMS.Repositories.Entities;

namespace PRN232.LMS.Repositories.Context;

public class LmsDbContext : DbContext
{
    public LmsDbContext(DbContextOptions<LmsDbContext> options) : base(options) { }

    public DbSet<Semester> Semesters { get; set; }
    public DbSet<Course> Courses { get; set; }
    public DbSet<Subject> Subjects { get; set; }
    public DbSet<Student> Students { get; set; }
    public DbSet<Enrollment> Enrollments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // === Semester Configuration ===
        modelBuilder.Entity<Semester>(e =>
        {
            e.HasKey(s => s.SemesterId);
            e.Property(s => s.SemesterName).IsRequired().HasMaxLength(100);
            e.Property(s => s.StartDate).IsRequired();
            e.Property(s => s.EndDate).IsRequired();
        });

        // === Course Configuration ===
        modelBuilder.Entity<Course>(e =>
        {
            e.HasKey(c => c.CourseId);
            e.Property(c => c.CourseName).IsRequired().HasMaxLength(100);
            e.HasOne(c => c.Semester)
             .WithMany(s => s.Courses)
             .HasForeignKey(c => c.SemesterId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // === Subject Configuration ===
        modelBuilder.Entity<Subject>(e =>
        {
            e.HasKey(s => s.SubjectId);
            e.Property(s => s.SubjectCode).IsRequired().HasMaxLength(20);
            e.Property(s => s.SubjectName).IsRequired().HasMaxLength(100);
            e.Property(s => s.Credit).IsRequired();
        });

        // === Student Configuration ===
        modelBuilder.Entity<Student>(e =>
        {
            e.HasKey(s => s.StudentId);
            e.Property(s => s.FullName).IsRequired().HasMaxLength(100);
            e.Property(s => s.Email).IsRequired().HasMaxLength(100);
            e.HasIndex(s => s.Email).IsUnique();
            e.Property(s => s.DateOfBirth).IsRequired();
        });

        // === Enrollment Configuration ===
        modelBuilder.Entity<Enrollment>(e =>
        {
            e.HasKey(en => en.EnrollmentId);
            e.Property(en => en.Status).IsRequired().HasMaxLength(20);
            e.HasOne(en => en.Student)
             .WithMany(s => s.Enrollments)
             .HasForeignKey(en => en.StudentId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(en => en.Course)
             .WithMany(c => c.Enrollments)
             .HasForeignKey(en => en.CourseId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // === Seed Data ===
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // --- Semesters (5) ---
        var semesters = new List<Semester>
        {
            new() { SemesterId = 1, SemesterName = "Spring 2023",  StartDate = new DateTime(2023, 1, 10), EndDate = new DateTime(2023, 5, 20) },
            new() { SemesterId = 2, SemesterName = "Summer 2023",  StartDate = new DateTime(2023, 6, 1),  EndDate = new DateTime(2023, 8, 31) },
            new() { SemesterId = 3, SemesterName = "Fall 2023",    StartDate = new DateTime(2023, 9, 4),  EndDate = new DateTime(2023, 12, 22) },
            new() { SemesterId = 4, SemesterName = "Spring 2024",  StartDate = new DateTime(2024, 1, 8),  EndDate = new DateTime(2024, 5, 18) },
            new() { SemesterId = 5, SemesterName = "Summer 2024",  StartDate = new DateTime(2024, 6, 3),  EndDate = new DateTime(2024, 8, 30) },
        };
        modelBuilder.Entity<Semester>().HasData(semesters);

        // --- Subjects (10) ---
        var subjects = new List<Subject>
        {
            new() { SubjectId = 1,  SubjectCode = "PRN231", SubjectName = "Application Development using .NET",   Credit = 3 },
            new() { SubjectId = 2,  SubjectCode = "PRN232", SubjectName = "Advanced Cross-Platform Application",  Credit = 3 },
            new() { SubjectId = 3,  SubjectCode = "SWD392", SubjectName = "Software Architecture and Design",     Credit = 3 },
            new() { SubjectId = 4,  SubjectCode = "PRJ301", SubjectName = "Java Web Application Development",     Credit = 3 },
            new() { SubjectId = 5,  SubjectCode = "DBI202", SubjectName = "Introduction to Database",             Credit = 3 },
            new() { SubjectId = 6,  SubjectCode = "MAE101", SubjectName = "Mathematics for Engineering",          Credit = 3 },
            new() { SubjectId = 7,  SubjectCode = "CEA201", SubjectName = "Computer Organization and Assembly",   Credit = 3 },
            new() { SubjectId = 8,  SubjectCode = "NWC202", SubjectName = "Computer Networking",                  Credit = 3 },
            new() { SubjectId = 9,  SubjectCode = "SWE201", SubjectName = "Introduction to Software Engineering", Credit = 3 },
            new() { SubjectId = 10, SubjectCode = "ITE302", SubjectName = "Ethics in IT",                         Credit = 3 },
        };
        modelBuilder.Entity<Subject>().HasData(subjects);

        // --- Courses (20: 4 per semester) ---
        var courses = new List<Course>
        {
            // Semester 1
            new() { CourseId = 1,  CourseName = "PRN231 - Spring 2023",  SemesterId = 1 },
            new() { CourseId = 2,  CourseName = "DBI202 - Spring 2023",  SemesterId = 1 },
            new() { CourseId = 3,  CourseName = "MAE101 - Spring 2023",  SemesterId = 1 },
            new() { CourseId = 4,  CourseName = "SWE201 - Spring 2023",  SemesterId = 1 },
            // Semester 2
            new() { CourseId = 5,  CourseName = "PRN232 - Summer 2023",  SemesterId = 2 },
            new() { CourseId = 6,  CourseName = "SWD392 - Summer 2023",  SemesterId = 2 },
            new() { CourseId = 7,  CourseName = "NWC202 - Summer 2023",  SemesterId = 2 },
            new() { CourseId = 8,  CourseName = "CEA201 - Summer 2023",  SemesterId = 2 },
            // Semester 3
            new() { CourseId = 9,  CourseName = "PRJ301 - Fall 2023",    SemesterId = 3 },
            new() { CourseId = 10, CourseName = "ITE302 - Fall 2023",    SemesterId = 3 },
            new() { CourseId = 11, CourseName = "MAE101 - Fall 2023",    SemesterId = 3 },
            new() { CourseId = 12, CourseName = "SWE201 - Fall 2023",    SemesterId = 3 },
            // Semester 4
            new() { CourseId = 13, CourseName = "PRN231 - Spring 2024",  SemesterId = 4 },
            new() { CourseId = 14, CourseName = "DBI202 - Spring 2024",  SemesterId = 4 },
            new() { CourseId = 15, CourseName = "NWC202 - Spring 2024",  SemesterId = 4 },
            new() { CourseId = 16, CourseName = "CEA201 - Spring 2024",  SemesterId = 4 },
            // Semester 5
            new() { CourseId = 17, CourseName = "PRN232 - Summer 2024",  SemesterId = 5 },
            new() { CourseId = 18, CourseName = "SWD392 - Summer 2024",  SemesterId = 5 },
            new() { CourseId = 19, CourseName = "PRJ301 - Summer 2024",  SemesterId = 5 },
            new() { CourseId = 20, CourseName = "ITE302 - Summer 2024",  SemesterId = 5 },
        };
        modelBuilder.Entity<Course>().HasData(courses);

        // --- Students (50) ---
        var firstNames = new[] { "Nguyen", "Tran", "Le", "Pham", "Hoang", "Vu", "Dang", "Bui", "Do", "Duong" };
        var lastNames  = new[] { "Van An", "Thi Binh", "Van Cuong", "Thi Dung", "Van Em", "Thi Phuong", "Minh Quan", "Thi Hoa", "Van Khanh", "Thi Lan", "Minh Long", "Thi Mai", "Van Nam", "Thi Oanh", "Van Phuc", "Thi Quyen", "Van Son", "Thi Thuy", "Van Uyen", "Thi Van", "Van Xuan", "Thi Yen", "Van Zung", "Thi Anh", "Van Bao" };

        var students = new List<Student>();
        for (int i = 1; i <= 50; i++)
        {
            var fn = firstNames[(i - 1) % firstNames.Length];
            var ln = lastNames[(i - 1) % lastNames.Length];
            students.Add(new Student
            {
                StudentId   = i,
                FullName    = $"{fn} {ln}",
                Email       = $"student{i:D2}@fpt.edu.vn",
                DateOfBirth = new DateTime(2002, (i % 12) + 1, (i % 28) + 1)
            });
        }
        modelBuilder.Entity<Student>().HasData(students);

        // --- Enrollments (500: 25 students × 20 courses, but avoid duplicates by distributing) ---
        var statuses = new[] { "Active", "Completed", "Withdrawn", "Pending" };
        var enrollments = new List<Enrollment>();
        int enrollId = 1;
        var random = new Random(42); // fixed seed for reproducibility
        var enrolledPairs = new HashSet<(int, int)>();

        // Ensure each course gets at least ~25 enrollments
        for (int courseId = 1; courseId <= 20; courseId++)
        {
            int count = 0;
            int attempts = 0;
            while (count < 25 && attempts < 200)
            {
                int studentId = random.Next(1, 51);
                var pair = (studentId, courseId);
                if (!enrolledPairs.Contains(pair))
                {
                    enrolledPairs.Add(pair);
                    enrollments.Add(new Enrollment
                    {
                        EnrollmentId = enrollId++,
                        StudentId    = studentId,
                        CourseId     = courseId,
                        EnrollDate   = new DateTime(2023, 1, 10).AddDays(random.Next(0, 365)),
                        Status       = statuses[random.Next(statuses.Length)]
                    });
                    count++;
                }
                attempts++;
            }
        }

        // Fill remaining to reach 500
        while (enrollments.Count < 500)
        {
            int studentId = random.Next(1, 51);
            int courseId  = random.Next(1, 21);
            var pair = (studentId, courseId);
            if (!enrolledPairs.Contains(pair))
            {
                enrolledPairs.Add(pair);
                enrollments.Add(new Enrollment
                {
                    EnrollmentId = enrollId++,
                    StudentId    = studentId,
                    CourseId     = courseId,
                    EnrollDate   = new DateTime(2023, 1, 10).AddDays(random.Next(0, 500)),
                    Status       = statuses[random.Next(statuses.Length)]
                });
            }
        }

        modelBuilder.Entity<Enrollment>().HasData(enrollments);
    }
}
