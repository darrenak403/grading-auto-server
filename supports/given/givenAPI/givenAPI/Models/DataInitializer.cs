using System;
using System.Collections.Generic;

namespace givenAPI.Models
{
    public static class DataInitializer
    {
        public static List<Instructor> Instructors = new List<Instructor> {
            new Instructor { InstructorId = 1, FullName = "TS. Tran Van Nam", Expertise = "Cong nghe phan mem", HireDate = new DateTime(2015,5,10) },
            new Instructor { InstructorId = 2, FullName = "ThS. Le Thi Binh", Expertise = "He thong thong tin", HireDate = new DateTime(2018,2,15) },
            new Instructor { InstructorId = 3, FullName = "GS. Nguyen Quoc Cuong", Expertise = "Tri tue nhan tao", HireDate = new DateTime(2010,11,20) },
            new Instructor { InstructorId = 4, FullName = "TS. Pham Minh Tuan", Expertise = "Mang may tinh", HireDate = new DateTime(2020,1,5) },
            new Instructor { InstructorId = 5, FullName = "ThS. Do Hong Hanh", Expertise = "An toan thong tin", HireDate = new DateTime(2021,9,12) }
        };

        public static List<Course> Courses = new List<Course> {
            new Course { CourseId = 1, CourseName = "Co so du lieu", Credits = 3, Department = "CNTT" },
            new Course { CourseId = 2, CourseName = "Lap trinh Web", Credits = 4, Department = "CNTT" },
            new Course { CourseId = 3, CourseName = "Toan roi rac", Credits = 3, Department = "Toan hoc" },
            new Course { CourseId = 4, CourseName = "Hoc may", Credits = 4, Department = "Khoa hoc du lieu" },
            new Course { CourseId = 5, CourseName = "Mang may tinh co ban", Credits = 3, Department = "Vien thong" }
        };

        public static List<CourseAssignment> CourseAssignments = new List<CourseAssignment> {
            new CourseAssignment { CourseId = 1, InstructorId = 1 }, new CourseAssignment { CourseId = 1, InstructorId = 2 },
            new CourseAssignment { CourseId = 2, InstructorId = 2 }, new CourseAssignment { CourseId = 2, InstructorId = 4 },
            new CourseAssignment { CourseId = 3, InstructorId = 3 }, new CourseAssignment { CourseId = 4, InstructorId = 3 },
            new CourseAssignment { CourseId = 4, InstructorId = 1 }, new CourseAssignment { CourseId = 5, InstructorId = 4 },
            new CourseAssignment { CourseId = 5, InstructorId = 5 }, new CourseAssignment { CourseId = 3, InstructorId = 5 }
        };

        public static List<ClassSection> ClassSections = new List<ClassSection> {
            new ClassSection { SectionId = 1, CourseId = 1, RoomNumber = "A1-201", Semester = "Hoc ky 1 2024", MaxCapacity = 50 },
            new ClassSection { SectionId = 2, CourseId = 2, RoomNumber = "B2-105", Semester = "Hoc ky 1 2024", MaxCapacity = 40 },
            new ClassSection { SectionId = 3, CourseId = 3, RoomNumber = "C3-303", Semester = "Hoc ky 2 2024", MaxCapacity = 60 },
            new ClassSection { SectionId = 4, CourseId = 4, RoomNumber = "Lab-01", Semester = "Hoc ky 2 2024", MaxCapacity = 30 },
            new ClassSection { SectionId = 5, CourseId = 5, RoomNumber = "A1-402", Semester = "Hoc ky 1 2024", MaxCapacity = 45 }
        };

        public static List<Student> Students = new List<Student> {
            new Student { StudentId = 1, StudentName = "Nguyen Hoang Long", Email = "longnh@student.edu.vn", DateOfBirth = new DateTime(2004,5,12) },
            new Student { StudentId = 2, StudentName = "Vu Minh Anh", Email = "anhvm@student.edu.vn", DateOfBirth = new DateTime(2004,8,20) },
            new Student { StudentId = 3, StudentName = "Le Bao Chau", Email = "chaulb@student.edu.vn", DateOfBirth = new DateTime(2003,12,1) },
            new Student { StudentId = 4, StudentName = "Dang Tien Dat", Email = "datdt@student.edu.vn", DateOfBirth = new DateTime(2004,1,15) },
            new Student { StudentId = 5, StudentName = "Tran Thu Ha", Email = "hatt@student.edu.vn", DateOfBirth = new DateTime(2004,10,30) }
        };

        public static List<Enrollment> Enrollments = new List<Enrollment> {
            new Enrollment { EnrollmentId = 1, StudentId = 1, SectionId = 1, RegistrationDate = new DateTime(2024,1,2), Grade = 8.5 },
            new Enrollment { EnrollmentId = 2, StudentId = 2, SectionId = 1, RegistrationDate = new DateTime(2024,1,2), Grade = 7.0 },
            new Enrollment { EnrollmentId = 3, StudentId = 3, SectionId = 2, RegistrationDate = new DateTime(2024,1,5), Grade = 9.0 },
            new Enrollment { EnrollmentId = 4, StudentId = 4, SectionId = 3, RegistrationDate = new DateTime(2024,2,10), Grade = null },
            new Enrollment { EnrollmentId = 5, StudentId = 5, SectionId = 4, RegistrationDate = new DateTime(2024,2,12), Grade = 6.5 },
            new Enrollment { EnrollmentId = 6, StudentId = 1, SectionId = 5, RegistrationDate = new DateTime(2024,1,3), Grade = 8.0 },
            new Enrollment { EnrollmentId = 7, StudentId = 2, SectionId = 2, RegistrationDate = new DateTime(2024,1,6), Grade = null },
            new Enrollment { EnrollmentId = 8, StudentId = 3, SectionId = 4, RegistrationDate = new DateTime(2024,2,15), Grade = null },
            new Enrollment { EnrollmentId = 9, StudentId = 4, SectionId = 1, RegistrationDate = new DateTime(2024,1,4), Grade = 7.5 },
            new Enrollment { EnrollmentId = 10, StudentId = 5, SectionId = 5, RegistrationDate = new DateTime(2024,1,5), Grade = 9.5 }
        };
    }
}